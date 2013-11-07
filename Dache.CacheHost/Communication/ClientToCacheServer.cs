using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Text;
using System.Threading;
using Dache.CacheHost.Storage;
using Dache.Communication;
using Dache.Core.Interfaces;
using Dache.Core.Routing;

namespace Dache.CacheHost.Communication
{
    /// <summary>
    /// The server for client to cache communication.
    /// </summary>
    public class ClientToCacheServer : IClientToCacheContract, IRunnable
    {
        // The cache server
        private readonly Socket _server = null;
        // The thread that accepts socket connections
        private readonly Thread _connectionAccepterThread = null;
        // The default cache item policy
        private static readonly CacheItemPolicy _defaultCacheItemPolicy = new CacheItemPolicy();
        // The manual reset event that indicates that a connection was received
        private readonly ManualResetEvent _connectionReceived = new ManualResetEvent(false);
        // The communication encoding
        private static readonly Encoding _communicationEncoding = Encoding.ASCII;
        // The communication delimiter - four 0 bytes in a row
        private static readonly byte[] _communicationDelimiter = new byte[] { 0, 0, 0, 0 };
        // The byte that represents a space
        private static readonly byte[] _spaceByte = _communicationEncoding.GetBytes(" ");
        // The communication protocol reserved byte count - 4 little endian bytes + 1 control byte
        private const int _communicationProtocolReservedBytesCount = 5;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="port">The port.</param>
        public ClientToCacheServer(int port)
        {
            // Establish the endpoint for the socket
            var ipHostInfo = Dns.GetHostEntry("localhost");
            var ipAddress = ipHostInfo.AddressList.First(i => i.AddressFamily == AddressFamily.InterNetwork);
            var localEndPoint = new IPEndPoint(ipAddress, port);

            // Create the TCP/IP socket
            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // Disable the Nagle algorithm
            _server.NoDelay = true;
            // Bind to endpoint
            _server.Bind(localEndPoint);

            // Define connection accepter thread
            _connectionAccepterThread = new Thread(ConnectionAccepterThread);
        }

        /// <summary>
        /// The thread that accepts connections.
        /// </summary>
        private void ConnectionAccepterThread()
        {
            while (true)
            {
                _connectionReceived.Reset();

                // Wait for a connection
                _server.BeginAccept(AcceptCallback, null);

                _connectionReceived.WaitOne();
            }
        }

        private void AcceptCallback(IAsyncResult asyncResult)
        {
            // Get the socket that handles the client request
            Socket handler = _server.EndAccept(asyncResult);

            // Signal the main thread to continue
            _connectionReceived.Set();

            // Create the state object
            StateObject state = new StateObject
            {
                WorkSocket = handler
            };

            handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            StateObject state = (StateObject)asyncResult.AsyncState;
            Socket handler = state.WorkSocket;

            int bytesRead = 0;

            // Read data
            if ((bytesRead = handler.EndReceive(asyncResult)) > 0 && state.TotalBytesToRead != 0)
            {
                // Check if we need to decode little endian
                if (state.TotalBytesToRead == -1)
                {
                    // We do
                    var littleEndianBytes = new byte[4];
                    Buffer.BlockCopy(state.Buffer, 0, littleEndianBytes, 0, 4);
                    // Set total bytes to read
                    state.TotalBytesToRead = LittleEndianToInt(littleEndianBytes);
                    // Set control byte value
                    state.ControlByteValue = state.Buffer[4];
                    // Take endian bytes and control byte off
                    bytesRead -= _communicationProtocolReservedBytesCount;
                    // Remove the first 4 bytes from the buffer
                    var strippedBuffer = new byte[512];
                    Buffer.BlockCopy(state.Buffer, _communicationProtocolReservedBytesCount, strippedBuffer, 0, bytesRead);
                    state.Buffer = strippedBuffer;
                }

                // Set total bytes read and buffer
                state.TotalBytesToRead -= bytesRead;
                state.Data = Combine(state.Data, state.Data.Length, state.Buffer, bytesRead);

                // Receive more data
                handler.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
                return;
            }

            // Otherwise we're done, so close the handler and parse the command
            handler.Close();

            // Get the command bytes
            var commandBytes = state.Data;
            // Parse command from bytes
        }

        private List<byte[]> ReceiveDelimitedCacheObjects(out string commandPrefix, byte[] response, int cacheKeysCount = 10)
        {
            commandPrefix = null;

            // Split response by delimiter
            var result = new List<byte[]>(cacheKeysCount);
            int lastDelimiterIndex = 0;

            for (int i = 0; i < response.Length; i++)
            {
                // Check for delimiter
                for (int d = 0; d < _communicationDelimiter.Length; d++)
                {
                    if (i + d >= response.Length || response[i + d] != _communicationDelimiter[d])
                    {
                        // Leave loop
                        break;
                    }

                    // Check if we found it
                    if (d == _communicationDelimiter.Length - 1)
                    {
                        // Check if first delimeter
                        if (lastDelimiterIndex == 0)
                        {
                            // Set command prefix
                            commandPrefix = _communicationEncoding.GetString(response, 0, i - 1);
                        }
                        // Not first delimiter
                        else
                        {
                            // Add current section to final result
                            var resultItem = new byte[i - lastDelimiterIndex];
                            Buffer.BlockCopy(response, lastDelimiterIndex, resultItem, 0, i - lastDelimiterIndex);
                            result.Add(resultItem);
                        }

                        // Now set last delimiter index and skip ahead by the delimiter's size
                        lastDelimiterIndex = i + d + 1;
                        // No need to iterate over the delimiter
                        i += d;
                    }
                }
            }
            return result;
        }

        private IEnumerable<KeyValuePair<string, byte[]>> ReceiveDelimitedCacheKeysAndObjects(out string commandPrefix, byte[] response, int cacheKeysCount = 10)
        {
            commandPrefix = null;

            // Split response by delimiter
            var result = new List<KeyValuePair<string, byte[]>>(cacheKeysCount);
            int lastDelimiterIndex = 0;
            bool isEvenDelimiter = true;
            string currentCacheKey = null;

            for (int i = 0; i < response.Length; i++)
            {
                // Check for delimiter
                for (int d = 0; d < _communicationDelimiter.Length; d++)
                {
                    if (i + d >= response.Length || response[i + d] != _communicationDelimiter[d])
                    {
                        // Leave loop
                        break;
                    }

                    // Check if we found it
                    if (d == _communicationDelimiter.Length - 1)
                    {
                        // Check if first delimeter
                        if (lastDelimiterIndex == 0)
                        {
                            // Set command prefix
                            commandPrefix = _communicationEncoding.GetString(response, 0, i - 1);
                        }
                        // Not first delimiter
                        else
                        {
                            // Check if even delimiter
                            if (isEvenDelimiter)
                            {
                                // Getting a cache key
                                currentCacheKey = _communicationEncoding.GetString(response, lastDelimiterIndex, i - lastDelimiterIndex);
                                isEvenDelimiter = false;
                            }
                            else
                            {
                                // Getting a cached object
                                var resultItem = new byte[i - lastDelimiterIndex];
                                Buffer.BlockCopy(response, lastDelimiterIndex, resultItem, 0, i - lastDelimiterIndex);
                                result.Add(new KeyValuePair<string, byte[]>(currentCacheKey, resultItem));

                                isEvenDelimiter = true;
                            }
                        }

                        // Now set last delimiter index and skip ahead by the delimiter's size
                        lastDelimiterIndex = i + d + 1;
                        // No need to iterate over the delimiter
                        i += d;
                    }
                }
            }
            return result;
        }

        int LittleEndianToInt(byte[] bytes)
        {
            return (bytes[3] << 24) | (bytes[2] << 16) | (bytes[1] << 8) | bytes[0];
        }

        byte[] IntToLittleEndian(int value)
        {
            byte[] bytes = new byte[4];
            bytes[0] = (byte)value;
            bytes[1] = (byte)(((uint)value >> 8) & 0xFF);
            bytes[2] = (byte)(((uint)value >> 16) & 0xFF);
            bytes[3] = (byte)(((uint)value >> 24) & 0xFF);
            return bytes;
        }

        public static byte[] Combine(byte[] first, byte[] second)
        {
            return Combine(first, first.Length, second, second.Length);
        }

        public static byte[] Combine(byte[] first, int firstLength, byte[] second, int secondLength)
        {
            byte[] ret = new byte[firstLength + secondLength];
            Buffer.BlockCopy(first, 0, ret, 0, firstLength);
            Buffer.BlockCopy(second, 0, ret, firstLength, secondLength);
            return ret;
        }

        /// <summary>
        /// Starts the cache server.
        /// </summary>
        public void Start()
        {
            // Listen for connections with a backlog of 10000
            _server.Listen(10000);
            // Start the connection accepter thread
            _connectionAccepterThread.Start();
        }

        /// <summary>
        /// Stops the cache server.
        /// </summary>
        public void Stop()
        {
            // Abort the connection accepter thread
            _connectionAccepterThread.Abort();
            // Shutdown and close the server socket
            _server.Shutdown(SocketShutdown.Both);
            _server.Close();
        }

        /// <summary>
        /// Gets the serialized object stored at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>The serialized object.</returns>
        public byte[] Get(string cacheKey)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return null;
            }

            // Try to get value
            return MemCacheContainer.Instance.Get(cacheKey);
        }

        /// <summary>
        /// Gets the serialized objects stored at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> GetMany(IEnumerable<string> cacheKeys)
        {
            // Sanitize
            if (cacheKeys == null)
            {
                return null;
            }

            var result = new List<byte[]>(10);

            // Iterate all cache keys
            foreach (var cacheKey in cacheKeys)
            {
                // Sanitize
                if (string.IsNullOrWhiteSpace(cacheKey))
                {
                    // Skip
                    continue;
                }

                // Try to get value
                var getResult = MemCacheContainer.Instance.Get(cacheKey);
                if (getResult != null)
                {
                    result.Add(getResult);
                }
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Gets all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> GetTagged(string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            // Compile a list of the serialized objects
            List<byte[]> result = new List<byte[]>(10);

            // Get the values
            var cacheKeys = TagRoutingTable.Instance.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    var cacheValue = MemCacheContainer.Instance.Get(cacheKey);
                    if (cacheValue == null)
                    {
                        continue;
                    }

                    result.Add(cacheValue);
                }
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject)
        {
            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, _defaultCacheItemPolicy);
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, DateTimeOffset absoluteExpiration)
        {
            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = absoluteExpiration
            };
            
            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, TimeSpan slidingExpiration)
        {
            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                SlidingExpiration = slidingExpiration
            };

            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
        }

        /// <summary>
        /// Adds or updates an interned serialized object in the cache at the given cache key.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        public void AddOrUpdateInterned(string cacheKey, byte[] serializedObject)
        {
            // Place object in cache
            MemCacheContainer.Instance.AddInterned(cacheKey, serializedObject);
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdateMany(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value);
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateMany(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, absoluteExpiration);
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateMany(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, slidingExpiration);
            }
        }

        /// <summary>
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdateManyInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateInterned(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value);
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key and associates it with the given tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(string cacheKey, byte[] serializedObject, string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKey, serializedObject);
                return;
            }

            AddOrUpdate(cacheKey, serializedObject);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key and associates it with the given tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateTagged(string cacheKey, byte[] serializedObject, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKey, serializedObject, absoluteExpiration);
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = absoluteExpiration
            };

            AddOrUpdate(cacheKey, serializedObject, absoluteExpiration);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key and associates it with the given tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(string cacheKey, byte[] serializedObject, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKey, serializedObject, slidingExpiration);
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                SlidingExpiration = slidingExpiration
            };

            AddOrUpdate(cacheKey, serializedObject, slidingExpiration);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
        }

        /// <summary>
        /// Adds or updates the interned serialized object in the cache at the given cache key and associates it with the given tag name.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTaggedInterned(string cacheKey, byte[] serializedObject, string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdateInterned(cacheKey, serializedObject);
                return;
            }

            AddOrUpdateInterned(cacheKey, serializedObject);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateManyTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdateMany(cacheKeysAndSerializedObjects);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName);
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateManyTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdateMany(cacheKeysAndSerializedObjects, absoluteExpiration);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName, absoluteExpiration);
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateManyTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdateMany(cacheKeysAndSerializedObjects, slidingExpiration);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName, slidingExpiration);
            }
        }

        /// <summary>
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateManyTaggedInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdateMany(cacheKeysAndSerializedObjects);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName);
            }
        }

        /// <summary>
        /// Removes the serialized object at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public void Remove(string cacheKey)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            // Remove object from cache
            MemCacheContainer.Instance.Remove(cacheKey);
        }

        /// <summary>
        /// Removes the serialized objects at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        public void RemoveMany(IEnumerable<string> cacheKeys)
        {
            // Sanitize
            if (cacheKeys == null)
            {
                throw new ArgumentNullException("cacheKeys");
            }

            // Iterate all cache keys
            foreach (var cacheKey in cacheKeys)
            {
                Remove(cacheKey);
            }
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        public void RemoveTagged(string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            // Remove them all
            var cacheKeys = TagRoutingTable.Instance.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    MemCacheContainer.Instance.Remove(cacheKey);
                }
            }
        }

        private class StateObject
        {
            public Socket WorkSocket = null;
            public const int BufferSize = 512;
            public byte[] Buffer = new byte[BufferSize];
            public byte[] Data = new byte[0];
            public int ControlByteValue = 0;
            public int TotalBytesToRead = -1;
        }
    }
}

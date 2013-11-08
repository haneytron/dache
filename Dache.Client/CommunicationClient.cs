using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Dache.CacheHost.Communication;

namespace Dache.Client
{
    /// <summary>
    /// Encapsulates a WCF cache host client.
    /// </summary>
    internal class CommunicationClient : ICacheHostContract
    {
        // The remote endpoint
        private readonly IPEndPoint _remoteEndPoint = null;
        // The socket for communication
        private Socket _client = null;
        // The cache host reconnect interval, in milliseconds
        private readonly int _hostReconnectIntervalMilliseconds = 0;
        // The communication encoding
        private static readonly Encoding _communicationEncoding = Encoding.ASCII;
        // The communication delimiter - four 0 bytes in a row
        private static readonly byte[] _communicationDelimiter = new byte[] { 0, 0, 0, 0 };
        // The byte that represents a space
        private static readonly byte[] _spaceByte = _communicationEncoding.GetBytes(" ");
        // The communication protocol control bytes default - 4 little endian bytes + 1 control byte
        private static readonly byte[] _controlBytesDefault = new byte[] { 0, 0, 0, 0, 0 };
        // The absolute expiration format
        private const string _absoluteExpirationFormat = "yyMMddhhmmss";

        // Whether or not the communication client is connected
        private volatile bool _isConnected = false;
        // The lock object used for reconnection
        private readonly object _reconnectLock = new object();
        // The timer used to reconnect to the server
        private readonly Timer _reconnectTimer = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        /// <param name="hostReconnectIntervalMilliseconds">The cache host reconnect interval, in milliseconds.</param>
        public CommunicationClient(string address, int port, int hostReconnectIntervalMilliseconds)
        {
            // Sanitize
            if (String.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "address");
            }
            if (port <= 0)
            {
                throw new ArgumentException("must be greater than 0", "port");
            }
            if (hostReconnectIntervalMilliseconds <= 0)
            {
                throw new ArgumentException("must be greater than 0", "hostReconnectIntervalMilliseconds");
            }

            // Establish the remote endpoint for the socket
            var ipHostInfo = Dns.GetHostEntry(address);
            var ipAddress = ipHostInfo.AddressList[0];
            _remoteEndPoint = new IPEndPoint(ipAddress, port);

            // Create the TCP/IP socket
            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // Disable the Nagle algorithm
            _client.NoDelay = true;

            // Set the cache host reconnect interval
            _hostReconnectIntervalMilliseconds = hostReconnectIntervalMilliseconds;

            // Initialize and configure the reconnect timer to immediately fire on a different thread
            _reconnectTimer = new Timer(ReconnectToServer, null, 0, _hostReconnectIntervalMilliseconds); 
        }

        /// <summary>
        /// Gets the serialized object stored at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>The serialized object.</returns>
        public byte[] Get(string cacheKey)
        {
            var result = Get(new[] { cacheKey });
            if (result.Count == 0)
            {
                return null;
            }
            return result[0];
        }

        /// <summary>
        /// Gets the serialized objects stored at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> Get(IEnumerable<string> cacheKeys)
        {
            // Sanitize
            if (cacheKeys == null)
            {
                throw new ArgumentNullException("cacheKeys");
            }
            if (!cacheKeys.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeys");
            }

            int cacheKeysCount = 0;

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("get ");
                        foreach (var cacheKey in cacheKeys)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKey);
                            cacheKeysCount++;
                        }
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeys);

                lock (_client)
                {
                    // Send
                    _client.Send(command);
                    // Receive
                    DelimiterType delimiterType;
                    var response = Receive(out delimiterType);
                    return ReceiveDelimitedCacheObjects(response, cacheKeysCount);
                }
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
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
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("get-tag {0}", tagName);
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.None);

                lock (_client)
                {
                    // Send
                    _client.Send(command);
                    // Receive
                    DelimiterType delimiterType;
                    var response = Receive(out delimiterType);
                    return ReceiveDelimitedCacheObjects(response);
                }
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject)
        {
            AddOrUpdate(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) });
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, DateTimeOffset absoluteExpiration)
        {
            AddOrUpdate(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, absoluteExpiration);
        }
        
        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, TimeSpan slidingExpiration)
        {
            AddOrUpdate(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, slidingExpiration);
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
            AddOrUpdateInterned(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) });
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set ");
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set {0} ", absoluteExpiration.ToString(_absoluteExpirationFormat));
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set {0} ", (int)slidingExpiration.TotalSeconds);
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdateInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set-intern ");
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
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
            AddOrUpdateTagged(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName);
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
            AddOrUpdateTagged(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName, absoluteExpiration);
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
            AddOrUpdateTagged(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName, slidingExpiration);
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
            AddOrUpdateTaggedInterned(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName);
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set-tag {0} ", tagName);
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }
                        
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set-tag {0} {1} ", tagName, absoluteExpiration.ToString(_absoluteExpirationFormat));
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }

                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set-tag {0} {1} ", tagName, (int)slidingExpiration.TotalSeconds);
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }

                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTaggedInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("set-tag-intern {0} ", tagName);
                        foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Key);
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKeyAndSerializedObjectKvp.Value);
                        }

                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeysAndObjects);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Removes the serialized object at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public void Remove(string cacheKey)
        {
            Remove(new[] { cacheKey });
        }

        /// <summary>
        /// Removes the serialized objects at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        public void Remove(IEnumerable<string> cacheKeys)
        {
            // Sanitize
            if (cacheKeys == null)
            {
                throw new ArgumentNullException("cacheKeys");
            }
            if (!cacheKeys.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeys");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("del ");
                        foreach (var cacheKey in cacheKeys)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(cacheKey);
                        }
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeys);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        public void RemoveTagged(string tagName)
        {
            RemoveTagged(new[] { tagName });
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag names.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        public void RemoveTagged(IEnumerable<string> tagNames)
        {
            // Sanitize
            if (tagNames == null)
            {
                throw new ArgumentNullException("tagNames");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    using (var streamWriter = new StreamWriter(memoryStream, _communicationEncoding))
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault, 0, _controlBytesDefault.Length);
                        streamWriter.Write("del-tag ");
                        foreach (var tagName in tagNames)
                        {
                            streamWriter.Write(_communicationDelimiter);
                            streamWriter.Write(tagName);
                        }
                        command = memoryStream.ToArray();
                    }
                }

                // Set control bytes
                SetControlBytes(command, DelimiterType.RepeatingCacheKeys);

                // Send Async as we don't want to wait for it
                _client.BeginSend(command, 0, command.Length, 0, SendAsyncCallback, _client);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Outputs a human-friendly cache host address and port.
        /// </summary>
        /// <returns>A string containing the cache host address and port.</returns>
        public override string ToString()
        {
            return _remoteEndPoint.Address + ":" + _remoteEndPoint.Port;
        }

        /// <summary>
        /// Event that fires when the cache client is disconnected from a cache host.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Event that fires when the cache client is successfully reconnected to a disconnected cache host.
        /// </summary>
        public event EventHandler Reconnected;

        /// <summary>
        /// Makes the client enter the disconnected state.
        /// </summary>
        private void DisconnectFromServer()
        {
            // Check if already disconnected - ahead of the lock for speed
            if (!_isConnected)
            {
                // Do nothing
                return;
            }

            // Obtain the reconnect lock to ensure that we only even enter the disconnected state once per disconnect
            lock (_reconnectLock)
            {
                // Double check if already disconnected
                if (!_isConnected)
                {
                    // Do nothing
                    return;
                }

                // Fire the disconnected event
                var disconnected = Disconnected;
                if (disconnected != null)
                {
                    disconnected(this, EventArgs.Empty);
                }

                // Set the reconnect timer to try reconnection in a configured interval
                _reconnectTimer.Change(_hostReconnectIntervalMilliseconds, _hostReconnectIntervalMilliseconds);

                // Set connected to false
                _isConnected = false;
            }
        }

        /// <summary>
        /// Connects or reconnects to the server.
        /// </summary>
        /// <param name="state">The state. Ignored but required for timer callback methods. Pass null.</param>
        private void ReconnectToServer(object state)
        {
            // Check if already reconnected - ahead of the lock for speed
            if (_isConnected)
            {
                // Do nothing
                return;
            }

            // Lock to ensure atomic operations
            lock (_reconnectLock)
            {
                // Double check if already reconnected
                if (_isConnected)
                {
                    // Do nothing
                    return;
                }

                // Close and abort the socket if needed
                try
                {
                    // Attempt the close
                    _client.Shutdown(SocketShutdown.Both);
                    _client.Close();
                }
                catch
                {
                    // Close failed, abort it
                    try
                    {
                        _client.Shutdown(SocketShutdown.Send);
                        _client.Close();
                    }
                    catch
                    {
                        // Still failed, ignore it
                    }
                }

                // Create the TCP/IP socket
                _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                // Disable the Nagle algorithm
                _client.NoDelay = true;

                // Attempt the actual reconnection
                try
                {
                    _client.Connect(_remoteEndPoint);
                }
                catch
                {
                    // Reconnection failed, so we're done (the Timer will retry)
                    return;
                }

                // If we got here, we're back online, adjust reconnected status
                _isConnected = true;

                // Disable the reconnect timer to stop trying to reconnect
                _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Fire the reconnected event
                var reconnected = Reconnected;
                if (reconnected != null)
                {
                    reconnected(this, EventArgs.Empty);
                }
            }
        }

        private byte[] Receive(out DelimiterType delimiterType)
        {
            delimiterType = DelimiterType.None;

            var buffer = new byte[512];
            var result = new byte[0];
            int bytesRead = 0;
            int totalBytesToRead = -1;

            while ((bytesRead = _client.Receive(buffer, (totalBytesToRead < 0 ? buffer.Length : Math.Min(512, totalBytesToRead)), SocketFlags.None)) > 0 && totalBytesToRead != 0)
            {
                // Check if we need to decode little endian and control byte
                if (totalBytesToRead == -1)
                {
                    // Parse out control bytes
                    buffer = RemoveControlByteValues(buffer, out totalBytesToRead, out delimiterType);
                    // Take control bytes off of bytes read
                    bytesRead -= _controlBytesDefault.Length;
                }

                // Set total bytes read and buffer
                totalBytesToRead -= bytesRead;
                result = Combine(result, result.Length, buffer, bytesRead);
            }

            return result;
        }

        private List<byte[]> ReceiveDelimitedCacheObjects(byte[] response, int cacheKeysCount = 10)
        {
            // Split response by delimiter
            var result = new List<byte[]>(cacheKeysCount);
            int lastDelimiterIndex = 0;

            // Start at index 1 to avoid the first delimiter
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
                        // Add current section to result if it isn't the very first delimiter
                        if (i != 0)
                        {
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

                // If we're at the end of the command, we need to add last section to the result
                if (i == response.Length - 1)
                {
                    var finalResultItem = new byte[i - lastDelimiterIndex];
                    Buffer.BlockCopy(response, lastDelimiterIndex, finalResultItem, 0, i - lastDelimiterIndex);
                    result.Add(finalResultItem);
                }
            }
            return result;
        }

        private void SendAsyncCallback(IAsyncResult asyncResult)
        {
            try
            {
                // Retrieve the socket from the state object
                Socket client = (Socket)asyncResult.AsyncState;

                // Complete sending the data to the remote device
                int bytesSent = client.EndSend(asyncResult);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        byte[] RemoveControlByteValues(byte[] command, out int messageLength, out DelimiterType delimiterType)
        {
            messageLength = (command[3] << 24) | (command[2] << 16) | (command[1] << 8) | command[0];
            delimiterType = (DelimiterType)command[4];
            var result = new byte[command.Length - _controlBytesDefault.Length];
            Buffer.BlockCopy(command, 5, result, 0, result.Length);
            return result;
        }

        void SetControlBytes(byte[] command, DelimiterType delimiterType)
        {
            var length = command.Length - _controlBytesDefault.Length;
            command[0] = (byte)length;
            command[1] = (byte)((length >> 8) & 0xFF);
            command[2] = (byte)((length >> 16) & 0xFF);
            command[3] = (byte)((length >> 24) & 0xFF);
            command[4] = Convert.ToByte((int)delimiterType);
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

        private enum DelimiterType
        {
            /// <summary>
            /// No delimiters used.
            /// </summary>
            None = 0,

            /// <summary>
            /// Repeating cache keys are delimited.
            /// </summary>
            RepeatingCacheKeys,

            /// <summary>
            /// Repeating cache objects are delimited.
            /// </summary>
            RepeatingCacheObjects,

            /// <summary>
            /// Repeating cache keys and objects are delimited in pairs.
            /// </summary>
            RepeatingCacheKeysAndObjects
        }
    }
}

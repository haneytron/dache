using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using Dache.CacheHost.Storage;
using Dache.Core.Interfaces;
using Dache.Core.Routing;

namespace Dache.CacheHost.Communication
{
    /// <summary>
    /// The server for client to cache communication.
    /// </summary>
    public class CacheHostServer : ICacheHostContract, IRunnable
    {
        // The cache server
        private readonly SocketRocker _server = null;
        // The local end point
        private readonly IPEndPoint _localEndPoint = null;
        // The maximum number of simultaneous connections
        private readonly int _maximumConnections = 0;
        // The message buffer size
        private readonly int _messageBufferSize = 0;
        // The thread used to receive messages
        private readonly Thread _receiveThread = null;

        // The connection receiver cancellation token source
        private readonly CancellationTokenSource _connectionReceiverCancellationTokenSource = new CancellationTokenSource();
        // The default cache item policy
        private static readonly CacheItemPolicy _defaultCacheItemPolicy = new CacheItemPolicy();

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="maximumConnections">The maximum number of simultaneous connections.</param>
        /// <param name="messageBufferSize">The buffer size to use for sending and receiving data.</param>
        public CacheHostServer(int port, int maximumConnections, int messageBufferSize)
        {
            // Set maximum connections and message buffer size
            _maximumConnections = maximumConnections;
            _messageBufferSize = messageBufferSize;

            // Establish the endpoint for the socket
            var ipHostInfo = Dns.GetHostEntry("localhost");
            var ipAddress = ipHostInfo.AddressList.First(i => i.AddressFamily == AddressFamily.InterNetwork);
            _localEndPoint = new IPEndPoint(ipAddress, port);

            // Define the socket rocker
            _server = new SocketRocker(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), messageBufferSize, maximumConnections, false);
            // Define the receive thread
            _receiveThread = new Thread(ReceiveThread);
        }

        private void ReceiveThread()
        {
            while (!_connectionReceiverCancellationTokenSource.IsCancellationRequested)
            {
                int threadId = 0;
                // Get a message - will block
                var command = _server.ServerReceive(out threadId);
                // Parse out the command byte
                DacheProtocolHelper.MessageType messageType = DacheProtocolHelper.MessageType.Literal;
                DacheProtocolHelper.ExtractControlByte(command, out messageType);
                // Get the command string skipping our control byte
                var commandString = DacheProtocolHelper.CommunicationEncoding.GetString(command, 1, command.Length - 1);
                var commandResult = ProcessCommand(commandString, messageType);
                if (commandResult != null)
                {
                    // Send the result if there is one
                    _server.ServerSend(commandResult, threadId);
                }
            }
        }
         
        private byte[] ProcessCommand(string command, DacheProtocolHelper.MessageType messageType)
        {
            string[] commandParts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> cacheKeys = null;
            IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndObjects = null;
            List<byte[]> results = null;
            var absoluteExpiration = DateTimeOffset.MinValue;
            int slidingExpiration = 0;
            byte[] commandResult = null;

            switch (messageType)
            {
                case DacheProtocolHelper.MessageType.Literal:
                {
                    // Sanitize the command
                    if (commandParts.Length != 2)
                    {
                        return null;
                    }

                    // The only command with no delimiter is get-tag so do that
                    var tagName = commandParts[1];
                    results = GetTagged(tagName);
                    // Structure the results for sending
                    using (var memoryStream = new MemoryStream())
                    {
                        memoryStream.WriteControlBytePlaceHolder();
                        foreach (var result in results)
                        {
                            memoryStream.WriteSpace();
                            memoryStream.WriteBase64(result);
                        }
                        commandResult = memoryStream.ToArray();
                    }

                    // Set control byte
                    commandResult.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheObjects);

                    break;
                }
                case DacheProtocolHelper.MessageType.RepeatingCacheKeys:
                {
                    // Determine command
                    if (command.StartsWith("get", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return null;
                        }

                        cacheKeys = commandParts.Skip(1).ToList();
                        results = Get(cacheKeys);
                        // Structure the results for sending
                        using (var memoryStream = new MemoryStream())
                        {
                            memoryStream.WriteControlBytePlaceHolder();
                            foreach (var result in results)
                            {
                                memoryStream.WriteSpace();
                                memoryStream.WriteBase64(result);
                            }
                            commandResult = memoryStream.ToArray();
                        }

                        // Set control byte
                        commandResult.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheObjects);
                    }
                    else if (command.StartsWith("del-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return null;
                        }

                        cacheKeys = commandParts.Skip(1).ToList();

                        foreach (var cacheKey in cacheKeys)
                        {
                            RemoveTagged(cacheKey);
                        }
                    }
                    else if (command.StartsWith("del", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return null;
                        }

                        cacheKeys = commandParts.Skip(1).ToList();
                        Remove(cacheKeys);
                    }

                    break;
                }
                case DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects:
                {
                    // Determine command
                    if (command.StartsWith("set-tag-intern", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only one method, so call it
                        if (commandParts.Length == 2)
                        {
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                            AddOrUpdateTaggedInterned(cacheKeysAndObjects, commandParts[1]);
                        }
                    }
                    else if (command.StartsWith("set-intern", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only one method, so call it
                        cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 1);
                        AddOrUpdateInterned(cacheKeysAndObjects);
                    }
                    else if (command.StartsWith("set-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check whether we have absolute or sliding options
                        if (commandParts.Length % 2 != 0)
                        {
                            // Regular set
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                            AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1]);
                        }
                        else
                        {
                            // Get absolute or sliding expiration
                            if (DateTimeOffset.TryParseExact(commandParts[2], DacheProtocolHelper.AbsoluteExpirationFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out absoluteExpiration))
                            {
                                // absolute expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], absoluteExpiration);
                            }
                            else if (int.TryParse(commandParts[2], out slidingExpiration))
                            {
                                // sliding expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], TimeSpan.FromSeconds(slidingExpiration));
                            }
                            else
                            {
                                // Neither worked, so it's a bad message
                                return null;
                            }
                        }
                    }
                    else if (command.StartsWith("set", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check whether we have absolute or sliding options
                        if (commandParts.Length % 2 != 0)
                        {
                            // Regular set
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 1);
                            AddOrUpdate(cacheKeysAndObjects);
                        }
                        else
                        {
                            // Get absolute or sliding expiration
                            if (DateTimeOffset.TryParseExact(commandParts[1], DacheProtocolHelper.AbsoluteExpirationFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out absoluteExpiration))
                            {
                                // absolute expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdate(cacheKeysAndObjects, absoluteExpiration);
                            }
                            else if (int.TryParse(commandParts[1], out slidingExpiration))
                            {
                                // sliding expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdate(cacheKeysAndObjects, TimeSpan.FromSeconds(slidingExpiration));
                            }
                            else
                            {
                                // Neither worked, so it's a bad message
                                return null;
                            }
                        }
                    }

                    break;
                }
            }
            
            // Return the result
            return commandResult;
        }

        private static IEnumerable<KeyValuePair<string, byte[]>> ParseCacheKeysAndObjects(string[] commandParts, int startIndex)
        {
            // Regular set
            var cacheKeysAndObjects = new List<KeyValuePair<string, byte[]>>(commandParts.Length / 2);
            for (int i = startIndex; i < commandParts.Length; i = i + 2)
            {
                cacheKeysAndObjects.Add(new KeyValuePair<string, byte[]>(commandParts[i], Convert.FromBase64String(commandParts[i + 1])));
            }
            return cacheKeysAndObjects;
        }

        /// <summary>
        /// Starts the cache server.
        /// </summary>
        public void Start()
        {
            // Listen for connections
            _server.Listen(_localEndPoint);

            // Perpetually receive
            _receiveThread.Start();
        }

        /// <summary>
        /// Stops the cache server.
        /// </summary>
        public void Stop()
        {
            // Issue cancellation to connection receiver thread
            _connectionReceiverCancellationTokenSource.Cancel();

            // Shutdown and close the server socket
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
        public List<byte[]> Get(IEnumerable<string> cacheKeys)
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
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
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
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, DateTimeOffset absoluteExpiration)
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
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, TimeSpan slidingExpiration)
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
        public void AddOrUpdateInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
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
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects);
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
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects, absoluteExpiration);
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
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects, slidingExpiration);
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
        public void AddOrUpdateTaggedInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects);
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
        public void Remove(IEnumerable<string> cacheKeys)
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
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using Dache.CacheHost.Configuration;
using Dache.CacheHost.Storage;
using Dache.Core.Interfaces;
using Dache.Core.Logging;
using Dache.Core.Routing;
using SimplSockets;

namespace Dache.CacheHost.Communication
{
<<<<<<< HEAD
    /// <summary>
    /// The server for client to cache communication.
    /// </summary>
    public class CacheHostServer : ICacheHostContract, IRunnable
    {
        // The mem cache
        private readonly IMemCache _memCache = null;
        // The tag routing table
        private readonly ITagRoutingTable _tagRoutingTable = null;
        // The cache server
        private readonly ISimplSocketServer _server = null;
        // The local end point
        private readonly IPEndPoint _localEndPoint = null;
        // The maximum number of simultaneous connections
        private readonly int _maximumConnections = 0;
        // The message buffer size
        private readonly int _messageBufferSize = 0;

        // The default cache item policy
        private static readonly CacheItemPolicy _defaultCacheItemPolicy = new CacheItemPolicy();

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="memCache">The mem cache.</param>
        /// <param name="tagRoutingTable">The tag routing table.</param>
        /// <param name="port">The port.</param>
        /// <param name="maximumConnections">The maximum number of simultaneous connections.</param>
        /// <param name="messageBufferSize">The buffer size to use for sending and receiving data.</param>
        public CacheHostServer(IMemCache memCache, ITagRoutingTable tagRoutingTable, int port, int maximumConnections, int messageBufferSize)
        {
            // Sanitize
            if (memCache == null)
            {
                throw new ArgumentNullException("memCache");
            }
            if (tagRoutingTable == null)
            {
                throw new ArgumentNullException("tagRoutingTable");
            }
            if (port <= 0)
            {
                throw new ArgumentException("cannot be <= 0", "port");
            }
            if (maximumConnections <= 0)
            {
                throw new ArgumentException("cannot be <= 0", "maximumConnections");
            }
            if (messageBufferSize < 256)
            {
                throw new ArgumentException("cannot be < 256", "messageBufferSize");
            }

            // Set the mem cache
            _memCache = memCache;
            // Set the tag routing table
            _tagRoutingTable = tagRoutingTable;

            // Set maximum connections and message buffer size
            _maximumConnections = maximumConnections;
            _messageBufferSize = messageBufferSize;

            // Establish the endpoint for the socket
            var ipHostInfo = Dns.GetHostEntry(string.Empty);
            // Listen on all interfaces
            _localEndPoint = new IPEndPoint(IPAddress.Any, port);

            // Define the server
            _server = SimplSocket.CreateServer(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), 
                (sender, e) => { /* Ignore it, client's toast */ }, ReceiveMessage, messageBufferSize, maximumConnections, false);
        }

        private void ReceiveMessage(object sender, MessageReceivedArgs e)
        {
            var command = e.ReceivedMessage.Message;
            // Parse out the command byte
            DacheProtocolHelper.MessageType messageType = DacheProtocolHelper.MessageType.Literal;
            DacheProtocolHelper.ExtractControlByte(command, out messageType);
            // Get the command string skipping our control byte
            var commandString = DacheProtocolHelper.CommunicationEncoding.GetString(command, 1, command.Length - 1);
            var commandResult = ProcessCommand(commandString, messageType);
            if (commandResult != null)
            {
                // Send the result if there is one
                _server.Reply(commandResult, e.ReceivedMessage);
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
                        for (int i = 0; i < results.Count; i++)
                        {
                            if (i != 0)
                            {
                                memoryStream.WriteSpace();
                            }
                            memoryStream.WriteBase64(results[i]);
                        }
                        commandResult = memoryStream.ToArray();
                    }

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
                            for (int i = 0; i < results.Count; i++)
                            {
                                if (i != 0)
                                {
                                    memoryStream.WriteSpace();
                                }
                                memoryStream.WriteBase64(results[i]);
                            }
                            commandResult = memoryStream.ToArray();
                        }
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
        }

        /// <summary>
        /// Stops the cache server.
        /// </summary>
        public void Stop()
        {
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
            return _memCache.Get(cacheKey);
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
                var getResult = _memCache.Get(cacheKey);
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
            var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    var cacheValue = _memCache.Get(cacheKey);
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
            _memCache.Add(cacheKey, serializedObject, _defaultCacheItemPolicy);
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
            _memCache.Add(cacheKey, serializedObject, cacheItemPolicy);
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
            _memCache.Add(cacheKey, serializedObject, cacheItemPolicy);
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
            _memCache.AddInterned(cacheKey, serializedObject);
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
            _tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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
            _tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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
            _tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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
            _tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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
            _memCache.Remove(cacheKey);
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
            var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    _memCache.Remove(cacheKey);
                }
            }
        }
    }
=======
	/// <summary>
	/// The server for client to cache communication.
	/// </summary>
	public class CacheHostServer : ICacheHostContract, IRunnable
	{
		// The mem cache
		private readonly IMemCache _memCache;
		// The tag routing table
		private readonly ITagRoutingTable _tagRoutingTable;
		// The cache server
		private readonly ISimplSocketServer _server;
		// The local end point
		private readonly IPEndPoint _localEndPoint;
		// The maximum number of simultaneous connections
		private readonly int _maximumConnections = 0; //not in use
		// The message buffer size
		private readonly int _messageBufferSize = 0; //not in use

		// The default cache item policy
		private static readonly CacheItemPolicy _defaultCacheItemPolicy = new CacheItemPolicy();

		// The logger
		private readonly ILogger _logger;

		/// <summary>
		/// The constructor.
		/// </summary>
		/// <param name="memCache">The mem cache.</param>
		/// <param name="tagRoutingTable">The tag routing table.</param>
		/// <param name="port">The port.</param>
		/// <param name="maximumConnections">The maximum number of simultaneous connections.</param>
		/// <param name="messageBufferSize">The buffer size to use for sending and receiving data.</param>
		public CacheHostServer(IMemCache memCache, ITagRoutingTable tagRoutingTable, int port, int maximumConnections, int messageBufferSize)
		{
			// Sanitize
			if (memCache == null)
			{
				throw new ArgumentNullException("memCache");
			}
			if (tagRoutingTable == null)
			{
				throw new ArgumentNullException("tagRoutingTable");
			}
			if (port <= 0)
			{
				throw new ArgumentException("cannot be <= 0", "port");
			}
			if (maximumConnections <= 0)
			{
				throw new ArgumentException("cannot be <= 0", "maximumConnections");
			}
			if (messageBufferSize < 256)
			{
				throw new ArgumentException("cannot be < 256", "messageBufferSize");
			}

			// Set the mem cache
			_memCache = memCache;
			// Set the tag routing table
			_tagRoutingTable = tagRoutingTable;

			// Set maximum connections and message buffer size
			_maximumConnections = maximumConnections;
			_messageBufferSize = messageBufferSize;

			// Establish the endpoint for the socket
			var ipHostInfo = Dns.GetHostEntry(string.Empty);
			var ipAddress = ipHostInfo.AddressList.First(i => i.AddressFamily == AddressFamily.InterNetwork);
			_localEndPoint = new IPEndPoint(ipAddress, port);

			// Define the server
			_server = SimplSocket.CreateServer(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), 
				(sender, e) => { /* Ignore it, client's toast */ }, ReceiveMessage, messageBufferSize, maximumConnections, false);

			// Load custom logging
			_logger = CustomLoggerLoader.LoadLogger();
		}

		private void ReceiveMessage(object sender, MessageReceivedArgs e)
		{
			var command = e.ReceivedMessage.Message;
			if (command == null || command.Length == 0)
			{
				_logger.Error("Dache.CacheHost.Communication.CacheHostServer.ReceiveMessage - command is null or empty", "command variable is null or empty, indicating an empty or invalid message");
				return;
			}

			// Parse out the command byte
			DacheProtocolHelper.MessageType messageType;
			command.ExtractControlByte(out messageType);
			// Get the command string skipping our control byte
			var commandString = DacheProtocolHelper.CommunicationEncoding.GetString(command, 1, command.Length - 1);
			var commandResult = ProcessCommand(commandString, messageType);
			if (commandResult != null)
			{
				// Send the result if there is one
				_server.Reply(commandResult, e.ReceivedMessage);
			}
		}
		 
		private byte[] ProcessCommand(string command, DacheProtocolHelper.MessageType messageType)
		{
			string[] commandParts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			List<byte[]> results;
			byte[] commandResult = null;

			switch (messageType)
			{
				case DacheProtocolHelper.MessageType.Literal:
				{
					if (command.StartsWith("get-tag", StringComparison.OrdinalIgnoreCase))
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
							for (int i = 0; i < results.Count; i++)
							{
								if (i != 0)
								{
									memoryStream.WriteSpace();
								}
								memoryStream.WriteBase64(results[i]);
							}
							commandResult = memoryStream.ToArray();
						}
					}
					else if (string.Compare(commandParts[0], "keys-tag", StringComparison.OrdinalIgnoreCase) == 0)
					{
						// Sanitize the command
						if (commandParts.Length != 3)
						{
							return null;
						}

						var tagName = commandParts[1];
						var searchPattern = commandParts[2];
						results = GetKeysTagged(tagName, searchPattern);
						// Structure the results for sending
						using (var memoryStream = new MemoryStream())
						{
							for (int i = 0; i < results.Count; i++)
							{
								if (i != 0)
								{
									memoryStream.WriteSpace();
								}
								memoryStream.WriteBase64(results[i]);
							}
							commandResult = memoryStream.ToArray();
						}
					}
					else if (string.Compare(commandParts[0], "keys", StringComparison.OrdinalIgnoreCase) == 0)
					{
						// Sanitize the command
						if (commandParts.Length != 2)
						{
							return null;
						}

						var searchPattern = commandParts[1];
						results = GetKeys(searchPattern);
						// Structure the results for sending
						using (var memoryStream = new MemoryStream())
						{
							for (int i = 0; i < results.Count; i++)
							{
								if (i != 0)
								{
									memoryStream.WriteSpace();
								}
								memoryStream.WriteBase64(results[i]);
							}
							commandResult = memoryStream.ToArray();
						}
					}
					else if (command.StartsWith("clear", StringComparison.OrdinalIgnoreCase))
					{
						Clear();
					}

					break;
				}
				case DacheProtocolHelper.MessageType.RepeatingCacheKeys:
				{
					// Determine command
					List<string> cacheKeys;
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
							for (int i = 0; i < results.Count; i++)
							{
								if (i != 0)
								{
									memoryStream.WriteSpace();
								}
								memoryStream.WriteBase64(results[i]);
							}
							commandResult = memoryStream.ToArray();
						}
					}
					else if (command.StartsWith("del-tag", StringComparison.OrdinalIgnoreCase))
					{
						// Sanitize the command
						//sample commands: 
						//del-tag * tagName1
						//del-tag * tagName1 tagName2
						//del-tag myPrefix* tagName1 tagName2
						if (commandParts.Length < 3)
						{
							return null; //TODO: should probably log this or do something with it
						}

						var keyPattern = commandParts[1];
						var tagNames = commandParts.Skip(2).ToList();
						foreach (var tagName in tagNames)
						{
							RemoveTagged(tagName, keyPattern);
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
					IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndObjects;
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
					else
					{
						int slidingExpiration;
						DateTimeOffset absoluteExpiration;
						if (command.StartsWith("set-tag", StringComparison.OrdinalIgnoreCase))
						{
							if (commandParts.Length < 4) //set-tag command should have no less than 4 parts: set-tag tagName keyName keyValue
								return null;

							// Check whether we have absolute or sliding options
							if (DateTimeOffset.TryParseExact(commandParts[2], DacheProtocolHelper.AbsoluteExpirationFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out absoluteExpiration))
							{
								// absolute expiration
								cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 3);
								AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], absoluteExpiration);
							}
							else if (int.TryParse(commandParts[2], out slidingExpiration)) //TODO: assumes a number/int won't be used for a key name, probably not a safe assumption
							{
								// sliding expiration
								cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 3);
								AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], TimeSpan.FromSeconds(slidingExpiration));
							}
							else
							{
								// Regular set
								cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
								AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1]);
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
					}

					break;
				}
			}
			
			// Return the result
			return commandResult;
		}

		private IEnumerable<KeyValuePair<string, byte[]>> ParseCacheKeysAndObjects(string[] commandParts, int startIndex)
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
		}

		/// <summary>
		/// Stops the cache server.
		/// </summary>
		public void Stop()
		{
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
			return _memCache.Get(cacheKey);
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
				var getResult = _memCache.Get(cacheKey);
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
			var result = new List<byte[]>(10);

			// Get the values
			var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName);
			if (cacheKeys != null)
			{
				foreach (var cacheKey in cacheKeys)
				{
					var cacheValue = _memCache.Get(cacheKey);
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
			_memCache.Add(cacheKey, serializedObject, _defaultCacheItemPolicy);
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
			_memCache.Add(cacheKey, serializedObject, cacheItemPolicy);
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
			_memCache.Add(cacheKey, serializedObject, cacheItemPolicy);
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
			_memCache.AddInterned(cacheKey, serializedObject);
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
			_tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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

			AddOrUpdate(cacheKey, serializedObject, absoluteExpiration);

			// Add to the local tag routing table
			_tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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

			AddOrUpdate(cacheKey, serializedObject, slidingExpiration);

			// Add to the local tag routing table
			_tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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
			_tagRoutingTable.AddOrUpdate(cacheKey, tagName);
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
			_memCache.Remove(cacheKey);
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

		public void RemoveTagged(string tagName)
		{
			RemoveTagged(tagName, "*");
		}

		/// <summary>
		/// Removes all serialized objects associated with the given tag name and with keys matching the given pattern.
		/// </summary>
		/// <param name="tagName">The tag name.</param>
		/// <param name="pattern">The search pattern (regex). Default is '*'</param>
		public void RemoveTagged(string tagName, string pattern)
		{
			// Sanitize
			if (string.IsNullOrWhiteSpace(tagName))
			{
				return;
			}

			// Remove them all
			var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName, pattern);
			if (cacheKeys == null) 
				return;

			foreach (var cacheKey in cacheKeys)
			{
				_memCache.Remove(cacheKey);
			}
		}

		/// <summary>
		/// Gets all keys associated with the given tag name and given search pattern.
		/// </summary>
		/// <param name="tagName">The tag name.</param>
		/// <param name="pattern">The search pattern</param>
		/// <returns>A list of the keys.</returns>
		public List<byte[]> GetKeysTagged(string tagName, string pattern)
		{
			// Sanitize
			if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(pattern))
			{
				_logger.Warn("GetKeysTagged", "tagName or pattern is null or whitespace. sorry I can't be more helpful.");
				return null;
			}

			// Compile a list of the keys
			var result = new List<byte[]>(10);

			// Get the values
			var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName);
			if (cacheKeys == null) 
				return result;

			foreach (var key in cacheKeys)
			{
				result.Add(DacheProtocolHelper.CommunicationEncoding.GetBytes(key));
			}

			// Return the result
			return result;
		}

		/// <summary>
		/// Gets all keys matching the specified pattern (supports Regex).
		/// </summary>
		/// <param name="pattern">The search pattern</param>
		/// <returns>A list of the keys matching the search pattern</returns>
		public List<byte[]> GetKeys(string pattern)
		{
			// Sanitize
			if (string.IsNullOrWhiteSpace(pattern))
			{
				_logger.Warn("GetKeys", "pattern is null or whitespace");
				return null;
			}

			// Compile a list of the keys
			var result = new List<byte[]>(10);

			// Get the values
			var cacheKeys = _memCache.Keys(pattern);
			if (cacheKeys != null)
			{
				foreach (var key in cacheKeys)
				{
					result.Add(DacheProtocolHelper.CommunicationEncoding.GetBytes(key));
				}
			}

			// Return the result
			return result;
		}

		/// <summary>
		/// Clears the cache
		/// </summary>
		public void Clear()
		{
			_memCache.Clear();
		}
	}
>>>>>>> 84d97747847fea0358be6004c059d50b388e1767
}

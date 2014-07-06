using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using Dache.CacheHost;
using Dache.CacheHost.Routing;
using Dache.CacheHost.Storage;
using Dache.Core.Logging;
using SimplSockets;

namespace Dache.Core.Communication
{
    /// <summary>
    /// The server for client to cache communication.
    /// </summary>
    internal class CacheHostServer : ICacheHostContract, IRunnable
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
        /// <param name="logger">The logger.</param>
        /// <param name="port">The port.</param>
        /// <param name="maximumConnections">The maximum number of simultaneous connections.</param>
        /// <param name="messageBufferSize">The buffer size to use for sending and receiving data.</param>
        public CacheHostServer(IMemCache memCache, ITagRoutingTable tagRoutingTable, ILogger logger, int port, int maximumConnections, int messageBufferSize)
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
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
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
            // Set the logger
            _logger = logger;

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
            // Sanitize
            if (command == null)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(command))
            {
                return null;
            }

            string[] commandParts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (commandParts.Length == 0)
            {
                return null;
            }

            List<byte[]> results;
            byte[] commandResult = null;

            switch (messageType)
            {
                case DacheProtocolHelper.MessageType.Literal:
                {
                    if (string.Equals(commandParts[0], "keys", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length != 2)
                        {
                            return null;
                        }

                        var pattern = commandParts[1];
                        results = GetCacheKeys(pattern);
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
                    else if (string.Equals(commandParts[0], "clear", StringComparison.OrdinalIgnoreCase))
                    {
                        Clear();
                    }

                    break;
                }
                case DacheProtocolHelper.MessageType.RepeatingCacheKeys:
                {
                    // Determine command
                    if (string.Equals(commandParts[0], "get", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return null;
                        }

                        var cacheKeys = commandParts.Skip(1);
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
                    if (string.Equals(commandParts[0], "get-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return null;
                        }

                        var tagNames = commandParts.Skip(1);
                        results = GetTagged(tagNames);
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
                    else if (string.Equals(commandParts[0], "del-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // sample commands: 
                        // del-tag * tagName1
                        // del-tag * tagName1 tagName2
                        // del-tag myPrefix* tagName1 tagName2

                        // Sanitize the command
                        if (commandParts.Length < 3)
                        {
                            return null;
                        }

                        var pattern = commandParts[1];
                        var tagNames = commandParts.Skip(2);
                        RemoveTagged(tagNames, pattern);
                    }
                    else if (string.Equals(commandParts[0], "del", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return null;
                        }

                        var cacheKeys = commandParts.Skip(1);
                        Remove(cacheKeys);
                    }
                    else if (string.Equals(commandParts[0], "keys-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 3)
                        {
                            return null;
                        }

                        var pattern = commandParts[1];
                        var tagNames = commandParts.Skip(2);
                        results = GetCacheKeysTagged(tagNames, pattern);
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

                    break;
                }
                case DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects:
                {
                    // Determine command
                    IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndObjects;
                    if (string.Equals(commandParts[0], "set-tag-intern", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only one method, so call it
                        if (commandParts.Length == 2)
                        {
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                            AddOrUpdateTaggedInterned(cacheKeysAndObjects, commandParts[1]);
                        }
                    }
                    else if (string.Equals(commandParts[0], "set-intern", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only one method, so call it
                        cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 1);
                        AddOrUpdateInterned(cacheKeysAndObjects);
                    }
                    else if (string.Equals(commandParts[0], "set-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // set-tag command should have no less than 4 parts: set-tag tagName keyName keyValue
                        if (commandParts.Length < 4)
                        {
                            return null;
                        }

                        // Check whether we have absolute or sliding options
                        if (commandParts.Length % 2 == 0)
                        {
                            // Regular set
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                            AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1]);
                        }
                        else
                        {
                            // Get absolute or sliding expiration
                            int slidingExpiration;
                            DateTimeOffset absoluteExpiration;
                            if (int.TryParse(commandParts[2], out slidingExpiration))
                            {
                                // Sliding expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 3);
                                AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], TimeSpan.FromSeconds(slidingExpiration));
                            }
                            else if (DateTimeOffset.TryParseExact(commandParts[2], DacheProtocolHelper.AbsoluteExpirationFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out absoluteExpiration))
                            {
                                // Absolute expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 3);
                                AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], absoluteExpiration);
                            }
                        }
                    }
                    else if (string.Equals(commandParts[0], "set", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check whether we have absolute or sliding options
                        if (commandParts.Length % 2 == 1)
                        {
                            // Regular set
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 1);
                            AddOrUpdate(cacheKeysAndObjects);
                        }
                        else
                        {
                            // Get absolute or sliding expiration
                            int slidingExpiration;
                            DateTimeOffset absoluteExpiration;
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
                        }
                    }

                    break;
                }
            }

            // Return the result - may be null if there was no valid message
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
        /// Gets all serialized objects associated with the given tag names.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> GetTagged(IEnumerable<string> tagNames)
        {
            // Sanitize
            if (tagNames == null)
            {
                return null;
            }

            // Compile a list of the serialized objects
            var result = new List<byte[]>(10);

            // Enumerate all tag names
            foreach (var tagName in tagNames)
            {
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
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
        {
            AddOrUpdate(cacheKeysAndSerializedObjects, null, _defaultCacheItemPolicy);
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, DateTimeOffset absoluteExpiration)
        {
            AddOrUpdate(cacheKeysAndSerializedObjects, null, new CacheItemPolicy { AbsoluteExpiration = absoluteExpiration });
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, TimeSpan slidingExpiration)
        {
            AddOrUpdate(cacheKeysAndSerializedObjects, null, new CacheItemPolicy { SlidingExpiration = slidingExpiration });
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
                // Place object in cache
                _memCache.AddInterned(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value);
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            AddOrUpdate(cacheKeysAndSerializedObjects, tagName, _defaultCacheItemPolicy);
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, DateTimeOffset absoluteExpiration)
        {
            AddOrUpdate(cacheKeysAndSerializedObjects, tagName, new CacheItemPolicy { AbsoluteExpiration = absoluteExpiration });
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration)
        {
            AddOrUpdate(cacheKeysAndSerializedObjects, tagName, new CacheItemPolicy { SlidingExpiration = slidingExpiration });
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

            AddOrUpdateInterned(cacheKeysAndSerializedObjects);

            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects);
                return;
            }

            // If a tag name was not sent, ignore it
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                // Add to the local tag routing table
                _tagRoutingTable.AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, tagName);
            }
        }

        private void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, CacheItemPolicy cacheItemPolicy)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (cacheItemPolicy == null)
            {
                throw new ArgumentNullException("cacheItemPolicy");
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                // Place object in cache
                _memCache.Add(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, cacheItemPolicy);

                // Check if adding tag
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    continue;
                }

                // Add to the local tag routing table
                _tagRoutingTable.AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, tagName);
            }
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
                _memCache.Remove(cacheKey);
            }
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag names and optionally with keys matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The regular expression search pattern. If no pattern is provided, default "*" (all) is used.</param>
        public void RemoveTagged(IEnumerable<string> tagNames, string pattern = "*")
        {
            // Sanitize
            if (tagNames == null)
            {
                return;
            }

            // Enumerate all tag names
            foreach (var tagName in tagNames)
            {
                var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName, pattern);
                if (cacheKeys == null || cacheKeys.Count == 0)
                {
                    continue;
                }

                // Enumerate all cache keys and remove
                foreach (var cacheKey in cacheKeys)
                {
                    _memCache.Remove(cacheKey);
                }
            }
        }

        /// <summary>
        /// Gets all cache keys, optionally matching the provided pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="pattern">The regular expression search pattern. If no pattern is provided, default "*" (all) is used.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<byte[]> GetCacheKeys(string pattern = "*")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(pattern))
            {
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
        /// Gets all cache keys associated with the given tag names and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<byte[]> GetCacheKeysTagged(IEnumerable<string> tagNames, string pattern = "*")
        {
            // Sanitize
            if (tagNames == null)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return null;
            }

            // Compile a list of the keys
            var result = new List<byte[]>(10);

            // Enumerate all tag names
            foreach (var tagName in tagNames)
            {
                // Get the values
                var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName);
                if (cacheKeys == null || cacheKeys.Count == 0)
                {
                    continue;
                }

                foreach (var key in cacheKeys)
                {
                    result.Add(DacheProtocolHelper.CommunicationEncoding.GetBytes(key));
                }
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear()
        {
            _memCache.Clear();
        }
    }
}


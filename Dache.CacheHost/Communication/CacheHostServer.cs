using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
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
        // The cache server socket
        private readonly ISimplSocketServer _server;
        // The local end point
        private readonly IPEndPoint _localEndPoint;
        // The maximum number of simultaneous connections
        private readonly int _maximumConnections = 0; //not in use
        // The message buffer size
        private readonly int _messageBufferSize = 0; //not in use

        // The default cache item policy
        private readonly CacheItemPolicy _defaultCacheItemPolicy = null;
        // The default removed callback cache item policy
        private readonly CacheItemPolicy _defaultRemovedCallbackCacheItemPolicy = null;

        // The logger
        private readonly ILogger _logger;

        // The invalid command string list
        private readonly List<string> _invalidCommandStringList = new List<string> { "invalid command" };

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

            // Set the default cache item policies
            _defaultCacheItemPolicy = new CacheItemPolicy();
            _defaultRemovedCallbackCacheItemPolicy = new CacheItemPolicy { RemovedCallback = CacheItemRemoved };

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
            _server = new SimplSocketServer(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), messageBufferSize, maximumConnections);

            // Hook into received message event
            _server.MessageReceived += ReceiveMessage;
            _server.Error += (sender, e) => { _logger.Warn("Dache Client Disconnected", e.Exception); };
        }

        private void ReceiveMessage(object sender, MessageReceivedArgs e)
        {
            var command = e.ReceivedMessage.Message;
            if (command == null || command.Length == 0)
            {
                _logger.Error("Dache.CacheHost.Communication.CacheHostServer.ReceiveMessage - command is null or empty", "command variable is null or empty, indicating an empty or invalid message");
                return;
            }

            // Get the command string skipping our control byte
            var commandString = DacheProtocolHelper.CommunicationEncoding.GetString(command);
            var commandResult = ProcessCommand(commandString);
            if (commandResult != null)
            {
                // Send the result if there is one
                _server.Reply(commandResult, e.ReceivedMessage);
            }
        }

        private byte[] ProcessCommand(string command)
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

            byte[] commandResult = null;

            // Determine command
            switch (commandParts[0].ToLowerInvariant())
            {
                case "clear":
                {
                    Clear();
                    break;
                }

                case "keys":
                {
                    // Sanitize the command
                    if (commandParts.Length < 2)
                    {
                        return null;
                    }

                    var pattern = commandParts[1];
                    List<string> results = null;

                    // Check for tags
                    if (commandParts.Length > 3 && string.Equals(commandParts[2], "-t", StringComparison.OrdinalIgnoreCase))
                    {
                        var tagNames = commandParts.Skip(3);
                        results = GetCacheKeysTagged(tagNames, pattern);
                    }
                    else
                    {
                        results = GetCacheKeys(pattern);
                    }

                    commandResult = CreateCommandResult(results);
                    break;
                }

                case "get":
                {
                    // Sanitize the command
                    if (commandParts.Length < 2)
                    {
                        return null;
                    }

                    List<byte[]> results = null;

                    // Check for tags
                    if (commandParts.Length > 3 && string.Equals(commandParts[2], "-t", StringComparison.OrdinalIgnoreCase))
                    {
                        var pattern = commandParts[1];
                        var tagNames = commandParts.Skip(3);
                        results = GetTagged(tagNames, pattern);
                    }
                    else
                    {
                        var cacheKeys = commandParts.Skip(1);
                        results = Get(cacheKeys);
                    }

                    commandResult = CreateCommandResult(results);
                    break;
                }

                case "del":
                {
                    // Sanitize the command
                    if (commandParts.Length < 2)
                    {
                        return null;
                    }

                    // Check for tags
                    if (commandParts.Length > 3 && string.Equals(commandParts[2], "-t", StringComparison.OrdinalIgnoreCase))
                    {
                        var pattern = commandParts[1];
                        var tagNames = commandParts.Skip(3);
                        RemoveTagged(tagNames, pattern);
                    }
                    else
                    {
                        var cacheKeys = commandParts.Skip(1);
                        Remove(cacheKeys);
                    }

                    break;
                }

                case "set":
                {
                    // Sanitize the command
                    if (commandParts.Length < 3)
                    {
                        return null;
                    }

                    // Set initial cache keys and objects index
                    int cacheKeysAndObjectsIndex = 1;

                    bool isInterned = false;
                    string tagName = null;
                    DateTimeOffset? absoluteExpiration = null;
                    TimeSpan? slidingExpiration = null;
                    bool notifyRemoved = false;

                    // Check for flags
                    for (int i = 1; i < commandParts.Length; i++)
                    {
                        switch (commandParts[i].ToLowerInvariant())
                        {
                            // Interned
                            case "-i":
                            {
                                isInterned = true;
                                cacheKeysAndObjectsIndex++;
                                break;
                            }
                            // Tag name
                            case "-t":
                            {
                                tagName = commandParts[i + 1];
                                cacheKeysAndObjectsIndex += 2;
                                i++;
                                break;
                            }
                            // Absolute expiration
                            case "-a":
                            {
                                absoluteExpiration = DateTimeOffset.ParseExact(commandParts[i + 1], DacheProtocolHelper.AbsoluteExpirationFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                                cacheKeysAndObjectsIndex += 2;
                                i++;
                                break;
                            }

                            // Sliding expiration
                            case "-s":
                            {
                                slidingExpiration = TimeSpan.FromSeconds(int.Parse(commandParts[i + 1]));
                                cacheKeysAndObjectsIndex += 2;
                                i++;
                                break;
                            }

                            // Callback
                            case "-c":
                            {
                                notifyRemoved = true;
                                cacheKeysAndObjectsIndex++;
                                break;
                            }

                            // Assumed to be cache key
                            default:
                            {
                                i = commandParts.Length;
                                break;
                            }
                        }
                    }

                    IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, cacheKeysAndObjectsIndex);

                    // Decide what to do
                    if (isInterned)
                    {
                        AddOrUpdate(cacheKeysAndObjects, tagName: tagName, isInterned: true);
                    }
                    else
                    {
                        if (absoluteExpiration.HasValue)
                        {
                            AddOrUpdate(cacheKeysAndObjects, tagName: tagName, absoluteExpiration: absoluteExpiration.Value, notifyRemoved: notifyRemoved);
                        }
                        else if (slidingExpiration.HasValue)
                        {
                            AddOrUpdate(cacheKeysAndObjects, tagName: tagName, slidingExpiration: slidingExpiration.Value, notifyRemoved: notifyRemoved);
                        }
                        else
                        {
                            AddOrUpdate(cacheKeysAndObjects, tagName: tagName, notifyRemoved: notifyRemoved);
                        }
                    }
                    break;
                }
                default:
                {
                    // Invalid command
                    commandResult = CreateCommandResult(_invalidCommandStringList);
                    break;
                }
            }

            // Return the result - may be null if there was no valid message
            return commandResult;
        }

        private byte[] CreateCommandResult(List<string> results)
        {
            // Structure the results for sending
            return DacheProtocolHelper.CommunicationEncoding.GetBytes(string.Join(" ", results));
        }

        private byte[] CreateCommandResult(List<byte[]> results)
        {
            // Sanitize
            if (results == null || results.Count == 0)
            {
                // Send smallest possible reply to indicate no results
                return new byte[1];
            }

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
                return memoryStream.ToArray();
            }
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

        private void CacheItemRemoved(CacheEntryRemovedArguments args)
        {
            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Write("expire");
                memoryStream.WriteSpace();
                memoryStream.Write(args.CacheItem.Key);
                command = memoryStream.ToArray();
            }

            // Notify all clients
            _server.Broadcast(command);
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
        /// Gets all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> GetTagged(IEnumerable<string> tagNames, string pattern = "*")
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
                        if (pattern == "*" || Regex.IsMatch(cacheKey, pattern))
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
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="slidingExpiration">The sliding expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="notifyRemoved">Whether or not to notify the client when the cached item is removed from the cache.</param>
        /// <param name="isInterned">Whether or not to intern the objects. NOTE: interned objects use significantly less memory when 
        /// placed in the cache multiple times however cannot expire or be evicted. You must remove them manually when appropriate 
        /// or else you will face a memory leak. If specified, absoluteExpiration, slidingExpiration, and notifyRemoved are ignored.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName = null, DateTimeOffset? absoluteExpiration = null, TimeSpan? slidingExpiration = null, bool notifyRemoved = false, bool isInterned = false)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }

            // Determine cache item policy
            CacheItemPolicy cacheItemPolicy;
            CacheEntryRemovedCallback cacheEntryRemovedCallback = null;

            if (notifyRemoved)
            {
                cacheItemPolicy = _defaultRemovedCallbackCacheItemPolicy;
                cacheEntryRemovedCallback = CacheItemRemoved;
            }
            else
            {
                cacheItemPolicy = _defaultCacheItemPolicy;
            }

            if (absoluteExpiration.HasValue)
            {
                cacheItemPolicy = new CacheItemPolicy { AbsoluteExpiration = absoluteExpiration.Value, RemovedCallback = cacheEntryRemovedCallback };
            }
            else if (slidingExpiration.HasValue)
            {
                cacheItemPolicy = new CacheItemPolicy { SlidingExpiration = slidingExpiration.Value, RemovedCallback = cacheEntryRemovedCallback };
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                // Place object in cache
                if (isInterned)
                {
                    _memCache.AddInterned(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value);
                }
                else
                {
                    _memCache.Add(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, cacheItemPolicy);
                }

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
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
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
                    if (pattern == "*" || Regex.IsMatch(cacheKey, pattern))
                    {
                        _memCache.Remove(cacheKey);
                    }
                }
            }
        }

        /// <summary>
        /// Gets all cache keys, optionally matching the provided pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<string> GetCacheKeys(string pattern = "*")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return null;
            }

            // Get the values
            var cacheKeys = _memCache.Keys(pattern);

            if (cacheKeys == null)
            {
                return null;
            }

            return cacheKeys;
        }

        /// <summary>
        /// Gets all cache keys associated with the given tag names and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<string> GetCacheKeysTagged(IEnumerable<string> tagNames, string pattern = "*")
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
            var result = new List<string>();

            // Enumerate all tag names
            foreach (var tagName in tagNames)
            {
                // Get the values
                var cacheKeys = _tagRoutingTable.GetTaggedCacheKeys(tagName);
                if (cacheKeys == null || cacheKeys.Count == 0)
                {
                    continue;
                }

                if (pattern == "*")
                {
                    result.AddRange(cacheKeys);
                    continue;
                }

                // Enumerate all cache keys and match pattern
                foreach (var cacheKey in cacheKeys)
                {
                    if (Regex.IsMatch(cacheKey, pattern))
                    {
                        result.Add(cacheKey);
                    }
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


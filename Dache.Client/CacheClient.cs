using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.Serialization;
using System.Threading;
using Dache.Client.Configuration;
using Dache.Client.Exceptions;
using Dache.Client.Serialization;
using Dache.Core.Communication;
using Dache.Core.Logging;
using SharpMemoryCache;
using SimplSockets;

namespace Dache.Client
{
    /// <summary>
    /// The client for cache host communication.
    /// </summary>
    public class CacheClient : ICacheClient
    {
        // The list of cache clients
        private readonly List<CacheHostBucket> _cacheHostLoadBalancingDistribution = new List<CacheHostBucket>(10);
        // The cache host bucket comparer
        private readonly IComparer<CacheHostBucket> _cacheHostBucketComparer = new CacheHostBucketComparer();
        // The lock used to ensure state
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // The local cache
        private readonly MemoryCache _localCache = null;
        // The local cache item absolute expiration seconds
        private readonly int _localCacheItemExpirationSeconds = 0;
        // The local cache name suffix - all instances of this class share the value to avoid duplication
        private static int _localCacheNameSuffix = 0;

        // The binary serializer
        private readonly IBinarySerializer _binarySerializer = null;
        // The logger
        private readonly ILogger _logger = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        public CacheClient()
        {
            // Load custom logging
            _logger = CustomTypesLoader.LoadLogger();

            // Configure custom serializer
            _binarySerializer = CustomTypesLoader.LoadSerializer();

            // Get the cache hosts from configuration
            var cacheHosts = CacheClientConfigurationSection.Settings.CacheHosts;
            // Get the cache host reconnect interval from configuration
            var hostReconnectIntervalSeconds = CacheClientConfigurationSection.Settings.HostReconnectIntervalSeconds;

            // Sanitize
            if (cacheHosts == null)
            {
                throw new ConfigurationErrorsException("At least one cache host must be specified in your application's configuration.");
            }

            // Initialize and configure the local cache
            var physicalMemoryLimitPercentage = CacheClientConfigurationSection.Settings.LocalCacheMemoryLimitPercentage;
            var cacheConfig = new NameValueCollection();
            cacheConfig.Add("pollingInterval", "00:00:15");
            cacheConfig.Add("cacheMemoryLimitMegabytes", "0");
            cacheConfig.Add("physicalMemoryLimitPercentage", physicalMemoryLimitPercentage.ToString(CultureInfo.InvariantCulture));
            // Increment the local cache name suffix to avoid overlapping local caches
            int localCacheNameSuffix = Interlocked.Increment(ref _localCacheNameSuffix);
            _localCache = new TrimmingMemoryCache("Dache Local Cache " + localCacheNameSuffix, cacheConfig);

            _localCacheItemExpirationSeconds = CacheClientConfigurationSection.Settings.LocalCacheAbsoluteExpirationSeconds;

            // Add the cache hosts to the cache client list
            foreach (CacheHostElement cacheHost in cacheHosts)
            {
                // Instantiate a cache host client container
                var clientContainer = new CommunicationClient(cacheHost.Address, cacheHost.Port, hostReconnectIntervalSeconds * 1000, 1000, 4096);

                // Hook up the disconnected and reconnected events
                clientContainer.Disconnected += OnClientDisconnected;
                clientContainer.Reconnected += OnClientReconnected;

                // Hook up the message receive event
                clientContainer.MessageReceived += ReceiveMessage;

                // Attempt to connect
                if (!clientContainer.Connect())
                {
                    // Skip it for now
                    continue;
                }

                // Add to the client list - constructor so no lock needed over the add here
                _cacheHostLoadBalancingDistribution.Add(new CacheHostBucket
                {
                    CacheHost = clientContainer
                });
            }

            // Now calculate the load balancing distribution
            CalculateCacheHostLoadBalancingDistribution();
        }

        /// <summary>
        /// Gets the object stored at the given cache key from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value or default for that type if the method returns false.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public bool TryGet<T>(string cacheKey, out T value)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (cacheKey.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "cacheKey");
            }

            // Do remote work
            List<byte[]> rawValues = null;

            do
            {
                var client = DetermineClient(cacheKey);

                try
                {
                    rawValues = client.Get(new[] { cacheKey });
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);

            // If we got nothing back, return false and the default value for the type;
            if (rawValues == null || rawValues.Count == 0)
            {
                value = default(T);
                return false;
            }

            // Deserialize
            try
            {
                value = (T)_binarySerializer.Deserialize(rawValues[0]);
                return true;
            }
            catch
            {
                // Log serialization error
                _logger.Error("Serialization Error", string.Format("The object at cache key \"{0}\" could not be deserialized to type {1}", cacheKey, typeof(T)));

                value = default(T);
                return false;
            }
        }

        /// <summary>
        /// Gets the objects stored at the given cache keys from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the objects stored at the cache keys, or null if none were found.</returns>
        public List<T> Get<T>(IEnumerable<string> cacheKeys)
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

            // Do remote work
            List<byte[]> rawResults = null;

            do
            {
                // Need to batch up requests
                var routingDictionary = new Dictionary<CommunicationClient, List<string>>(_cacheHostLoadBalancingDistribution.Count);
                List<string> clientCacheKeys = null;
                foreach (var cacheKey in cacheKeys)
                {
                    if (cacheKey.IndexOf(' ') != -1)
                    {
                        // Log cache key error
                        _logger.Error("Cache Key Error", string.Format("Cache key \"{0}\" contains one or more spaces", cacheKey));
                        continue;
                    }

                    // Get the communication client
                    var client = DetermineClient(cacheKey);
                    if (!routingDictionary.TryGetValue(client, out clientCacheKeys))
                    {
                        clientCacheKeys = new List<string>(10);
                        routingDictionary.Add(client, clientCacheKeys);
                    }

                    clientCacheKeys.Add(cacheKey);
                }

                try
                {
                    // Now we've batched them, do the work
                    rawResults = null;
                    foreach (var routingDictionaryEntry in routingDictionary)
                    {
                        if (rawResults == null)
                        {
                            rawResults = routingDictionaryEntry.Key.Get(routingDictionaryEntry.Value);
                            continue;
                        }
                        rawResults.AddRange(routingDictionaryEntry.Key.Get(routingDictionaryEntry.Value));
                    }

                    // If we got here we did all of the work successfully
                    break;
                }
                catch
                {
                    // Rebalance and try again if a cache host could not be reached
                }
            } while (true);

            // If we got nothing back, return null
            if (rawResults == null)
            {
                return null;
            }

            var results = new List<T>(rawResults.Count);

            // Deserialize
            for (int i = 0; i < rawResults.Count; i++)
            {
                try
                {
                    results.Add((T)_binarySerializer.Deserialize(rawResults[i]));
                }
                catch
                {
                    results.Add(default(T));
                    // Log serialization error
                    _logger.Error("Serialization Error", string.Format("The object returned in a Get call at index {0} could not be deserialized to type {1}", i, typeof(T)));
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the objects stored at the given tag name from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="tagName">The tag name.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>A list of the objects stored at the tag name, or null if none were found.</returns>
        public List<T> GetTagged<T>(string tagName, string pattern = "*")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }
            if (tagName.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "tagName");
            }

            // Do remote work
            IList<byte[]> rawResults = null;

            do
            {
                // Use the tag's client
                var client = DetermineClient(tagName);

                try
                {
                    rawResults = client.GetTagged(new[] { tagName }, pattern: pattern);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);

            // If we got nothing back, return null
            if (rawResults == null)
            {
                return null;
            }

            var results = new List<T>(rawResults.Count);

            // Deserialize
            for (int i = 0; i < rawResults.Count; i++)
            {
                try
                {
                    results.Add((T)_binarySerializer.Deserialize(rawResults[i]));
                }
                catch
                {
                    results.Add(default(T));
                    // Log serialization error
                    _logger.Error("Serialization Error", string.Format("An object returned in a GetTagged call at index {0} could not be deserialized to type {1}", i, typeof(T)));
                }
            }

            return results;
        }

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="slidingExpiration">The sliding expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="notifyRemoved">Whether or not to notify the client when the cached item is removed from the cache.</param>
        /// <param name="isInterned">Whether or not to intern the objects. NOTE: interned objects use significantly less memory when 
        /// placed in the cache multiple times however cannot expire or be evicted. You must remove them manually when appropriate 
        /// or else you will face a memory leak. If specified, absoluteExpiration, slidingExpiration, and notifyRemoved are ignored.</param>
        public void AddOrUpdate(string cacheKey, object value, string tagName = null, DateTimeOffset? absoluteExpiration = null, TimeSpan? slidingExpiration = null, bool notifyRemoved = false, bool isInterned = false)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (cacheKey.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (tagName != null && tagName.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "tagName");
            }

            byte[] bytes = null;
            try
            {
                // Serialize
                bytes = _binarySerializer.Serialize(value);
            }
            catch (Exception ex)
            {
                throw new SerializationException("value could not be serialized.", ex);
            }

            do
            {
                var client = DetermineClient(cacheKey);

                try
                {
                    client.AddOrUpdate(new[] { new KeyValuePair<string, byte[]>(cacheKey, bytes) }, tagName: tagName, absoluteExpiration: absoluteExpiration, slidingExpiration: slidingExpiration, notifyRemoved: notifyRemoved, isInterned: isInterned);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="slidingExpiration">The sliding expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="notifyRemoved">Whether or not to notify the client when the cached item is removed from the cache.</param>
        /// <param name="isInterned">Whether or not to intern the objects. NOTE: interned objects use significantly less memory when 
        /// placed in the cache multiple times however cannot expire or be evicted. You must remove them manually when appropriate 
        /// or else you will face a memory leak. If specified, absoluteExpiration, slidingExpiration, and notifyRemoved are ignored.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName = null, DateTimeOffset? absoluteExpiration = null, TimeSpan? slidingExpiration = null, bool notifyRemoved = false, bool isInterned = false)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            if (!cacheKeysAndObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
            }
            if (tagName != null && tagName.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "tagName");
            }

            var routingDictionary = new Dictionary<CommunicationClient, List<KeyValuePair<string, byte[]>>>(_cacheHostLoadBalancingDistribution.Count);
            List<KeyValuePair<string, byte[]>> clientCacheKeysAndObjects = null;
            byte[] bytes = null;

            do
            {
                foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
                {
                    if (cacheKeyAndObjectKvp.Key.IndexOf(' ') != -1)
                    {
                        // Log cache key error
                        _logger.Error("Cache Key Error", string.Format("Cache key \"{0}\" contains one or more spaces", cacheKeyAndObjectKvp.Key));
                        continue;
                    }

                    try
                    {
                        // Serialize
                        bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    }
                    catch
                    {
                        // Log serialization error
                        _logger.Error("Serialization Error", string.Format("An object added via an AddOrUpdateMany call at cache key \"{0}\" could not be serialized", cacheKeyAndObjectKvp.Key));
                        continue;
                    }

                    // Get the communication client
                    var client = DetermineClient(cacheKeyAndObjectKvp.Key);
                    if (!routingDictionary.TryGetValue(client, out clientCacheKeysAndObjects))
                    {
                        clientCacheKeysAndObjects = new List<KeyValuePair<string, byte[]>>(10);
                        routingDictionary.Add(client, clientCacheKeysAndObjects);
                    }

                    clientCacheKeysAndObjects.Add(new KeyValuePair<string, byte[]>(cacheKeyAndObjectKvp.Key, bytes));
                }

                // Ensure we're doing something
                if (clientCacheKeysAndObjects.Count == 0)
                {
                    return;
                }

                try
                {
                    foreach (var routingDictionaryEntry in routingDictionary)
                    {
                        routingDictionaryEntry.Key.AddOrUpdate(routingDictionaryEntry.Value, tagName: tagName, absoluteExpiration: absoluteExpiration, slidingExpiration: slidingExpiration, notifyRemoved: notifyRemoved, isInterned: isInterned);
                    }

                    // If we got here we did all of the work successfully
                    break;
                }
                catch
                {
                    // Rebalance and try again if a cache host could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Removes the object at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public void Remove(string cacheKey)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (cacheKey.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "cacheKey");
            }

            do
            {
                var client = DetermineClient(cacheKey);

                try
                {
                    client.Remove(new[] { cacheKey });
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Removes the objects at the given cache keys from the cache.
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

            do
            {
                // Need to batch up requests
                var routingDictionary = new Dictionary<CommunicationClient, List<string>>(_cacheHostLoadBalancingDistribution.Count);
                List<string> clientCacheKeys = null;
                foreach (var cacheKey in cacheKeys)
                {
                    if (cacheKey.IndexOf(' ') != -1)
                    {
                        // Log cache key error
                        _logger.Error("Cache Key Error", string.Format("Cache key \"{0}\" contains one or more spaces", cacheKey));
                        continue;
                    }

                    // Get the communication client
                    var client = DetermineClient(cacheKey);
                    if (!routingDictionary.TryGetValue(client, out clientCacheKeys))
                    {
                        clientCacheKeys = new List<string>(10);
                        routingDictionary.Add(client, clientCacheKeys);
                    }

                    clientCacheKeys.Add(cacheKey);
                }

                try
                {
                    // Now we've batched them, do the work
                    foreach (var routingDictionaryEntry in routingDictionary)
                    {
                        routingDictionaryEntry.Key.Remove(routingDictionaryEntry.Value);
                    }

                    // If we got here we did all of the work successfully
                    break;
                }
                catch
                {
                    // Rebalance and try again if a cache host could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag name and optionally with keys matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        public void RemoveTagged(string tagName, string pattern = "*")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }
            if (tagName.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "tagName");
            }

            do
            {
                var client = DetermineClient(tagName);

                try
                {
                    client.RemoveTagged(new[] { tagName }, pattern);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag names and optionally with keys matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        public void RemoveTagged(IEnumerable<string> tagNames, string pattern = "*")
        {
            // Sanitize
            if (tagNames == null)
            {
                throw new ArgumentNullException("tagNames");
            }
            if (!tagNames.Any())
            {
                throw new ArgumentException("must have at least one element", "tagNames");
            }
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }

            do
            {
                // Need to batch up requests
                var routingDictionary = new Dictionary<CommunicationClient, List<string>>(_cacheHostLoadBalancingDistribution.Count);
                List<string> clientTagNames = null;
                foreach (var tagName in tagNames)
                {
                    if (tagName.IndexOf(' ') != -1)
                    {
                        // Log tag name error
                        _logger.Error("Tag Name Error", string.Format("Tag name \"{0}\" contains one or more spaces", tagName));
                        continue;
                    }

                    // Get the communication client
                    var client = DetermineClient(tagName);
                    if (!routingDictionary.TryGetValue(client, out clientTagNames))
                    {
                        clientTagNames = new List<string>(10);
                        routingDictionary.Add(client, clientTagNames);
                    }

                    clientTagNames.Add(tagName);
                }

                try
                {
                    // Now we've batched them, do the work
                    foreach (var routingDictionaryEntry in routingDictionary)
                    {
                        routingDictionaryEntry.Key.RemoveTagged(routingDictionaryEntry.Value, pattern);
                    }

                    // If we got here we did all of the work successfully
                    break;
                }
                catch
                {
                    // Rebalance and try again if a cache host could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Gets all cache keys, optionally matching the provided pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<string> GetCacheKeys(string pattern = "*")
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }

            do
            {
                List<string> results = new List<string>();

                // Enumerate all cache hosts
                try
                {
                    foreach (var communicationClient in _cacheHostLoadBalancingDistribution)
                    {
                        var rawResults = communicationClient.CacheHost.GetCacheKeys(pattern);

                        // Ensure we got some results
                        if (rawResults == null)
                        {
                            // Skip client
                            continue;
                        }

                        // Add to overall results
                        results.AddRange(rawResults);
                    }

                    return results;
                }
                catch
                {
                    // Rebalance and try again if a cache host could not be reached or the list changed
                }
            } while (true);
        }

        /// <summary>
        /// Gets all cache keys associated with the given tag name and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<string> GetCacheKeysTagged(string tagName, string pattern = "*")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }
            if (tagName.IndexOf(' ') != -1)
            {
                throw new ArgumentException("cannot contain spaces", "tagName");
            }

            do
            {
                var client = DetermineClient(tagName);

                try
                {
                    var rawResults = client.GetCacheKeys(pattern);

                    // Ensure we got some results
                    if (rawResults == null)
                    {
                        return null;
                    }

                    return rawResults;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
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
                throw new ArgumentNullException("tagNames");
            }
            if (!tagNames.Any())
            {
                throw new ArgumentException("must have at least one element", "tagNames");
            }
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }

            do
            {
                List<string> results = new List<string>(100);

                // Need to batch up requests
                var routingDictionary = new Dictionary<CommunicationClient, List<string>>(_cacheHostLoadBalancingDistribution.Count);
                List<string> clientTagNames = null;
                foreach (var tagName in tagNames)
                {
                    if (tagName.IndexOf(' ') != -1)
                    {
                        // Log tag name error
                        _logger.Error("Tag Name Error", string.Format("Tag name \"{0}\" contains one or more spaces", tagName));
                        continue;
                    }

                    // Get the communication client
                    var client = DetermineClient(tagName);
                    if (!routingDictionary.TryGetValue(client, out clientTagNames))
                    {
                        clientTagNames = new List<string>(10);
                        routingDictionary.Add(client, clientTagNames);
                    }

                    clientTagNames.Add(tagName);
                }

                try
                {
                    // Now we've batched them, do the work
                    foreach (var routingDictionaryEntry in routingDictionary)
                    {
                        var rawResults = routingDictionaryEntry.Key.GetCacheKeysTagged(routingDictionaryEntry.Value, pattern);

                        // Ensure we got some results for this host
                        if (rawResults == null)
                        {
                            // Skip host
                            continue;
                        }

                        // Add to overall results
                        results.AddRange(rawResults);
                    }

                    // Ensure we got some results
                    if (results.Count == 0)
                    {
                        return null;
                    }

                    return results;
                }
                catch
                {
                    // Rebalance and try again if a cache host could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear()
        {
            do
            {
                // Enumerate all cache hosts
                try
                {
                    foreach (var communicationClient in _cacheHostLoadBalancingDistribution)
                    {
                        communicationClient.CacheHost.Clear();
                    }

                    // If we got here we succeeded
                    break;
                }
                catch
                {
                    // Rebalance and try again if a cache host could not be reached or the list changed
                }   
            } while (true);
        }

        /// <summary>
        /// Event that fires when the cache client is disconnected from a cache host.
        /// </summary>
        public event EventHandler HostDisconnected;

        /// <summary>
        /// Event that fires when the cache client is successfully reconnected to a disconnected cache host.
        /// </summary>
        public event EventHandler HostReconnected;

        /// <summary>
        /// Event that fires when a cached item has expired out of the cache.
        /// </summary>
        public event EventHandler<CacheItemExpiredArgs> CacheItemExpired;

        /// <summary>
        /// Triggered when a client is disconnected from a cache host.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnClientDisconnected(object sender, EventArgs e)
        {
            var client = (CommunicationClient)sender;

            // Remove the communication client from the list of clients
            _lock.EnterWriteLock();

            try
            {
                var cacheHostClient = _cacheHostLoadBalancingDistribution.FirstOrDefault(i => i.CacheHost.Equals(client));
                if (cacheHostClient == null)
                {
                    // Already done
                    return;
                }

                _cacheHostLoadBalancingDistribution.Remove(cacheHostClient);

                // Calculate load balancing distribution
                CalculateCacheHostLoadBalancingDistribution();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            // Log the event
            _logger.Warn("Cache Host Disconnected", "The cache client has been disconnected from the cache host located at " + client.ToString() + " - it will be reconnected automatically as soon it can be successfully contacted.");

            var hostDisconnected = HostDisconnected;
            if (hostDisconnected != null)
            {
                hostDisconnected(sender, e);
            }
        }

        /// <summary>
        /// Triggered when a client is reconnected from a cache host.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event args.</param>
        private void OnClientReconnected(object sender, EventArgs e)
        {
            var client = (CommunicationClient)sender;

            // Add the communication client to the list of clients
            _lock.EnterWriteLock();

            try
            {
                _cacheHostLoadBalancingDistribution.Add(new CacheHostBucket
                {
                    CacheHost = client
                });

                // Calculate load balancing distribution
                CalculateCacheHostLoadBalancingDistribution();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            // Log the event
            _logger.Warn("Cache Host Reconnected", "The cache client has successfully reconnected to the cache host located at " + client.ToString());

            var hostReconnected = HostReconnected;
            if (hostReconnected != null)
            {
                hostReconnected(sender, e);
            }
        }

        /// <summary>
        /// Calculates the cache host load balancing distribution by considering the average object count across all hosts as well as the cached object count 
        /// at each of the hosts.
        /// </summary>
        private void CalculateCacheHostLoadBalancingDistribution()
        {
            // Get the number of cache hosts
            var registeredCacheHostCount = _cacheHostLoadBalancingDistribution.Count;

            // Reorder cache hosts so that all clients always use the same order
            _cacheHostLoadBalancingDistribution.Sort(_cacheHostBucketComparer);

            int x = 0;
            // Iterate all cache hosts in the load balancing distribution
            for (int i = 0; i < _cacheHostLoadBalancingDistribution.Count; i++)
            {
                // Get the current cache host bucket
                var cacheHostBucket = _cacheHostLoadBalancingDistribution[i];

                // Determine current range
                int currentMinimum = (int)((long)(x * uint.MaxValue) / registeredCacheHostCount) - int.MaxValue - 1;
                // If not first iteration
                if (x > 0)
                {
                    // Add 1
                    currentMinimum++;
                }
                x++;
                int currentMaximum = (int)((long)(x * uint.MaxValue) / registeredCacheHostCount) - int.MaxValue - 1;

                // Update values
                cacheHostBucket.MinValue = currentMinimum;
                cacheHostBucket.MaxValue = currentMaximum;
            }
        }

        /// <summary>
        /// Determines the cache host client based on the cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>The cache host client.</returns>
        private CommunicationClient DetermineClient(string cacheKey)
        {
            _lock.EnterReadLock();

            try
            {
                // Ensure a client is available
                if (_cacheHostLoadBalancingDistribution.Count == 0)
                {
                    throw new NoCacheHostsAvailableException("There are no reachable cache hosts available. Verify your client settings and ensure that all cache hosts can be successfully communicated with from this client.");
                }

                // Compute hash code
                var hashCode = ComputeHashCode(cacheKey);
                var index = BinarySearch(hashCode);

                return _cacheHostLoadBalancingDistribution[index].CacheHost;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Computes an integer hash code for a cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>A hash code.</returns>
        private static int ComputeHashCode(string cacheKey)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in cacheKey)
                {
                    // Multiply by c to add greater variation
                    hash = (hash * 23 + c) * c;
                }
                return hash;
            }
        }

        /// <summary>
        /// Computers an integer hash code that is guarantee to be identical for the same set of cache keys presented in any order.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A hash code.</returns>
        private static int ComputeOrderIndependentHashCode(IEnumerable<string> cacheKeys)
        {
            int resultHash = 0;
            foreach (var cacheKey in cacheKeys)
            {
                int hash = 17;
                unchecked
                {
                    foreach (char c in cacheKey)
                    {
                        // Multiply by c to add greater variation
                        hash = (hash * 23 + c) * c;
                    }
                }
                resultHash ^= hash;
            }

            return resultHash;
        }

        /// <summary>
        /// Binary searches the cache host load balancing distribution for the index of the matching cache host.
        /// </summary>
        /// <param name="hashCode">The hash code.</param>
        /// <returns>A negative value if no cache host applies, otherwise the index of the cache host.</returns>
        private int BinarySearch(int hashCode)
        {
            // Find the middle of the list, rounded down
            var middleIndex = _cacheHostLoadBalancingDistribution.Count / 2;
            // Do the binary search recursively
            return BinarySearchRecursive(hashCode, middleIndex);
        }

        /// <summary>
        /// Recursively binary searches the cache host load balancing distribution for the index of the matching cache host.
        /// </summary>
        /// <param name="hashCode">The hash code.</param>
        /// <param name="currentIndex">The current index.</param>
        /// <returns>A negative value if no cache host applies, otherwise the index of the cache host.</returns>
        private int BinarySearchRecursive(int hashCode, int currentIndex)
        {
            var currentCacheHost = _cacheHostLoadBalancingDistribution[currentIndex];
            if (currentCacheHost.MinValue > hashCode)
            {
                // Go left
                return BinarySearchRecursive(hashCode, currentIndex / 2);
            }
            if (currentCacheHost.MaxValue < hashCode)
            {
                // Go right
                return BinarySearchRecursive(hashCode, (int)(currentIndex * 1.5));
            }

            // Otherwise check if we're all done
            if (currentCacheHost.MinValue <= hashCode && currentCacheHost.MaxValue >= hashCode)
            {
                return currentIndex;
            }

            // If we got here it doesn't exist, return the one's complement of where we are which will be negative
            return ~currentIndex;
        }

        private void ReceiveMessage(object sender, MessageReceivedArgs e)
        {
            var command = e.ReceivedMessage.Message;
            if (command == null || command.Length == 0)
            {
                throw new InvalidOperationException("Dache.CacheHost.Communication.CacheHostServer.ReceiveMessage - command variable is null or empty, indicating an empty or invalid message");
            }

            // Get the command string skipping our control byte
            var commandString = DacheProtocolHelper.CommunicationEncoding.GetString(command);

            // Right now this is only used for invalidating cache keys, so there will never be a reply
            ProcessCommand(commandString);
        }

        private void ProcessCommand(string command)
        {
            // Sanitize
            if (command == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            string[] commandParts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (commandParts.Length == 0)
            {
                return;
            }

            // Determine command
            if (string.Equals(commandParts[0], "expire", StringComparison.OrdinalIgnoreCase))
            {
                // Sanitize the command
                if (commandParts.Length < 2)
                {
                    return;
                }

                // Invalidate local cache keys
                foreach (var cacheKey in commandParts.Skip(1))
                {
                    _localCache.Remove(cacheKey);

                    // Fire the cache item expired event
                    var cacheItemExpired = CacheItemExpired;
                    if (cacheItemExpired != null)
                    {
                        cacheItemExpired(this, new CacheItemExpiredArgs(cacheKey));
                    }
                }
            }
        }

        /// <summary>
        /// Provides cache host and bucket range information
        /// </summary>
        private class CacheHostBucket
        {
            /// <summary>
            /// The cache host.
            /// </summary>
            public CommunicationClient CacheHost { get; set; }

            /// <summary>
            /// The minimum value of the range.
            /// </summary>
            public int MinValue { get; set; }

            /// <summary>
            /// The maximum value of the range.
            /// </summary>
            public int MaxValue { get; set; }
        }

        /// <summary>
        /// A cache host bucket comparer that compares cache host buckets by the underlying communication client's friendly address and port string.
        /// </summary>
        private class CacheHostBucketComparer : IComparer<CacheHostBucket>
        {
            /// <summary>
            /// Compares two cache host buckets.
            /// </summary>
            /// <param name="x">The first cache host bucket.</param>
            /// <param name="y">The second cache host bucket.</param>
            /// <returns>-1 if x is less than y, 1 is x is greater than x, or 1 if x equals y.</returns>
            public int Compare(CacheHostBucket x, CacheHostBucket y)
            {
                return string.Compare(x.CacheHost.ToString(), y.CacheHost.ToString());
            }
        }
    }
}

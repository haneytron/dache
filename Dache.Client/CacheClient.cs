using Dache.Client.Configuration;
using Dache.Client.Serialization;
using Dache.Core.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using Dache.Client.Exceptions;
using System.Collections.Specialized;
using System.Runtime.Caching;

namespace Dache.Client
{
    /// <summary>
    /// The client for cache host communication.
    /// </summary>
    public class CacheClient : ICacheClient
    {
        // The list of cache clients
        private readonly List<CacheHostBucket> _cacheHostLoadBalancingDistribution = new List<CacheHostBucket>(10);
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
        /// <param name="binarySerializer">The custom binary serializer to use. The custom binary serializer must be thread safe. Pass null to use the default serializer.</param>
        /// <param name="logger">The custom logger to use. The custom logger must be thread safe. Pass null to use the default logger.</param>
        public CacheClient(IBinarySerializer binarySerializer = null, ILogger logger = null)
        {
            // Assign custom serializer and logger
            _binarySerializer = binarySerializer ?? new BinarySerializer();
            _logger = logger ?? new EventViewerLogger("Cache Client", "Dache");

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
            cacheConfig.Add("physicalMemoryLimitPercentage", physicalMemoryLimitPercentage.ToString());
            // Increment the local cache name suffix to avoid overlapping local caches
            int localCacheNameSuffix = Interlocked.Increment(ref _localCacheNameSuffix);
            _localCache = new MemoryCache("Dache Local Cache " + localCacheNameSuffix, cacheConfig);

            _localCacheItemExpirationSeconds = CacheClientConfigurationSection.Settings.LocalCacheAbsoluteExpirationSeconds;

            // Add the cache hosts to the cache client list
            foreach (CacheHostElement cacheHost in cacheHosts.Cast<CacheHostElement>().OrderBy(i => i.Address).ThenBy(i => i.Port))
            {
                // Instantiate a cache host client container
                var clientContainer = new CommunicationClient(cacheHost.Address, cacheHost.Port, hostReconnectIntervalSeconds * 1000, 1000, 4096);

                // Hook up the disconnected and reconnected events
                clientContainer.Disconnected += OnClientDisconnected;
                clientContainer.Reconnected += OnClientReconnected;

                // Add to the client list - constructor so no lock needed over the add here
                _cacheHostLoadBalancingDistribution.Add(new CacheHostBucket 
                {
                    CacheHost = clientContainer
                });
            }

            // Now calculate the load balancing distribution
            CalculateCacheHostLoadBalancingDistribution();

            // Now connect to each cache host
            for (int i = 0; i < _cacheHostLoadBalancingDistribution.Count; i++)
            {
                var cacheHostBucket = _cacheHostLoadBalancingDistribution[i];
                try
                {
                    cacheHostBucket.CacheHost.Connect();
                }
                catch
                {
                    i--;
                }
            }
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

            byte[] rawValue = null;

            do
            {
                var client = DetermineClient(cacheKey);

                try
                {
                    rawValue = client.Get(cacheKey);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);

            // If we got nothing back, return false and the default value for the type;
            if (rawValue == null)
            {
                value = default(T);
                return false;
            }

            // Deserialize
            try
            {
                value = (T)_binarySerializer.Deserialize(rawValue);
                return true;
            }
            catch
            {
                // Log serialization error
                _logger.Error("Serialization Error", "The object at cache key \"" + cacheKey + "\" could not be deserialized to type " + typeof(T));

                value = default(T);
                return false;
            }
        }
        
        /// <summary>
        /// Gets the object stored at the given cache key from the local cache. If it is not found in the local 
        /// cache, the object is retrieved remotely and cached locally for subsequent local lookups.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value or default for that type if the method returns false.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public bool TryGetLocal<T>(string cacheKey, out T value)
        {
            var localCacheKey = "cachekey:" + cacheKey;

            // Try and get from local cache
            object result = _localCache.Get(localCacheKey);
            if (result != null)
            {
                value = (T)result;
                return true;
            }

            // Call usual TryGet
            bool boolResult = TryGet(cacheKey, out value);
            if (boolResult)
            {
                // Cache locally
                _localCache.Add(localCacheKey, value, new CacheItemPolicy { AbsoluteExpiration = DateTime.Now.AddSeconds(_localCacheItemExpirationSeconds) });
            }

            return boolResult;
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

            List<byte[]> rawResults = null;
            do
            {
                // Need to batch up requests
                var routingDictionary = new Dictionary<CommunicationClient, List<string>>(_cacheHostLoadBalancingDistribution.Count);
                List<string> clientCacheKeys = null;
                foreach (var cacheKey in cacheKeys)
                {
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
                    _logger.Error("Serialization Error", "The object returned in a Get call at index " + i + " could not be deserialized to type " + typeof(T));
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the objects stored at the given cache keys from the local cache. If they are not found in the local 
        /// cache, the objects are retrieved remotely and cached locally for subsequent local lookups.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the objects stored at the cache keys, or null if none were found.</returns>
        public List<T> GetLocal<T>(IEnumerable<string> cacheKeys)
        {
            // Create a cache key that is a unique order-independent hash code of all cache keys
            var hash = 0;
            foreach (var cacheKey in cacheKeys)
            {
                int h = cacheKey.GetHashCode();
                if (h != 0)
                    hash = unchecked(hash * h);
            }
            var orderIndependentCacheKey = "getmany:" + hash.ToString();

            // Try and get from local cache
            List<T> result = _localCache.Get(orderIndependentCacheKey) as List<T>;
            if (result != null)
            {
                return result;
            }

            // Call usual Get
            result = Get<T>(cacheKeys);
            if (result != null)
            {
                // Cache locally
                _localCache.Add(orderIndependentCacheKey, result, new CacheItemPolicy { AbsoluteExpiration = DateTime.Now.AddSeconds(_localCacheItemExpirationSeconds) });
            }

            return result;
        }

        /// <summary>
        /// Gets the objects stored at the given tag name from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="tagName">The tag name.</param>
        /// <returns>A list of the objects stored at the tag name, or null if none were found.</returns>
        public List<T> GetTagged<T>(string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            IList<byte[]> rawResults = null;

            do
            {
                // Use the tag's client
                var client = DetermineClient(tagName);

                try
                {
                    rawResults = client.GetTagged(tagName);
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
                    _logger.Error("Serialization Error", "An object returned in a GetTagged call at index " + i + " could not be deserialized to type " + typeof(T));
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the objects stored at the given tag name from the local cache. If they are not found in the local 
        /// cache, the objects are retrieved remotely and cached locally for subsequent local lookups.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="tagName">The tag name.</param>
        /// <returns>A list of the objects stored at the tag name, or null if none were found.</returns>
        public List<T> GetTaggedLocal<T>(string tagName)
        {
            // Create a cache key for tag name
            var cacheKey = "tag:" + tagName;

            // Try and get from local cache
            List<T> result = _localCache.Get(tagName) as List<T>;
            if (result != null)
            {
                return result;
            }

            // Call usual GetTagged
            result = GetTagged<T>(tagName);
            if (result != null)
            {
                // Cache locally
                _localCache.Add(cacheKey, result, new CacheItemPolicy { AbsoluteExpiration = DateTime.Now.AddSeconds(_localCacheItemExpirationSeconds) });
            }

            return result;
        }

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        public void AddOrUpdate(string cacheKey, object value)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
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
                    client.AddOrUpdate(cacheKey, bytes);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(string cacheKey, object value, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
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
                    client.AddOrUpdate(cacheKey, bytes, absoluteExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(string cacheKey, object value, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
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
                    client.AddOrUpdate(cacheKey, bytes, slidingExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates an interned object in the cache at the given cache key.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        public void AddOrUpdateInterned(string cacheKey, object value)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
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
                    client.AddOrUpdateInterned(cacheKey, bytes);
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
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects)
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

            var routingDictionary = new Dictionary<CommunicationClient, List<KeyValuePair<string, byte[]>>>(_cacheHostLoadBalancingDistribution.Count);
            List<KeyValuePair<string, byte[]>> clientCacheKeysAndObjects = null;
            byte[] bytes = null;

            do
            {
                foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
                {
                    try
                    {
                        // Serialize
                        // TODO: don't reserialize on a failure
                        bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    }
                    catch
                    {
                        // Log serialization error
                        _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
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
                        routingDictionaryEntry.Key.AddOrUpdate(routingDictionaryEntry.Value);
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
        /// Adds or updates many objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, DateTimeOffset absoluteExpiration)
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

            var routingDictionary = new Dictionary<CommunicationClient, List<KeyValuePair<string, byte[]>>>(_cacheHostLoadBalancingDistribution.Count);
            List<KeyValuePair<string, byte[]>> clientCacheKeysAndObjects = null;
            byte[] bytes = null;

            do
            {
                foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
                {
                    try
                    {
                        // Serialize
                        // TODO: don't reserialize on a failure
                        bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    }
                    catch
                    {
                        // Log serialization error
                        _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
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
                        routingDictionaryEntry.Key.AddOrUpdate(routingDictionaryEntry.Value, absoluteExpiration);
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
        /// Adds or updates many objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, TimeSpan slidingExpiration)
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

            var routingDictionary = new Dictionary<CommunicationClient, List<KeyValuePair<string, byte[]>>>(_cacheHostLoadBalancingDistribution.Count);
            List<KeyValuePair<string, byte[]>> clientCacheKeysAndObjects = null;
            byte[] bytes = null;

            do
            {
                foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
                {
                    try
                    {
                        // Serialize
                        // TODO: don't reserialize on a failure
                        bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    }
                    catch
                    {
                        // Log serialization error
                        _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
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
                        routingDictionaryEntry.Key.AddOrUpdate(routingDictionaryEntry.Value, slidingExpiration);
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
        /// Adds or updates the interned objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        public void AddOrUpdateInterned(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects)
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

            var routingDictionary = new Dictionary<CommunicationClient, List<KeyValuePair<string, byte[]>>>(_cacheHostLoadBalancingDistribution.Count);
            List<KeyValuePair<string, byte[]>> clientCacheKeysAndObjects = null;
            byte[] bytes = null;

            do
            {
                foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
                {
                    try
                    {
                        // Serialize
                        // TODO: don't reserialize on a failure
                        bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    }
                    catch
                    {
                        // Log serialization error
                        _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
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
                        routingDictionaryEntry.Key.AddOrUpdateInterned(routingDictionaryEntry.Value);
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
        /// Adds or updates an object in the cache at the given cache key with the associated tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(string cacheKey, object value, string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
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
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTagged(cacheKey, bytes, tagName);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key with the associated tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateTagged(string cacheKey, object value, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
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
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTagged(cacheKey, bytes, tagName, absoluteExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key with the associated tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(string cacheKey, object value, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
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
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTagged(cacheKey, bytes, tagName, slidingExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates the interned object in the cache at the given cache key with the associated tag name.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTaggedInterned(string cacheKey, object value, string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
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
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTaggedInterned(cacheKey, bytes, tagName);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count();
            if (count == 0)
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            var list = new List<KeyValuePair<string, byte[]>>(count);

            foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
            {
                byte[] bytes = null;
                try
                {
                    // Serialize
                    bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    // Add to list
                    list.Add(new KeyValuePair<string, byte[]>(cacheKeyAndObjectKvp.Key, bytes));
                }
                catch
                {
                    // Log serialization error
                    _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
                }
            }

            // Ensure we're doing something
            if (list.Count == 0)
            {
                return;
            }

            do
            {
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTagged(list, tagName);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count();
            if (count == 0)
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            var list = new List<KeyValuePair<string, byte[]>>(count);

            foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
            {
                byte[] bytes = null;
                try
                {
                    // Serialize
                    bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    // Add to list
                    list.Add(new KeyValuePair<string, byte[]>(cacheKeyAndObjectKvp.Key, bytes));
                }
                catch
                {
                    // Log serialization error
                    _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
                }
            }

            // Ensure we're doing something
            if (list.Count == 0)
            {
                return;
            }

            do
            {
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTagged(list, tagName, absoluteExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count();
            if (count == 0)
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            var list = new List<KeyValuePair<string, byte[]>>(count);

            foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
            {
                byte[] bytes = null;
                try
                {
                    // Serialize
                    bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    // Add to list
                    list.Add(new KeyValuePair<string, byte[]>(cacheKeyAndObjectKvp.Key, bytes));
                }
                catch
                {
                    // Log serialization error
                    _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call  at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
                }
            }

            // Ensure we're doing something
            if (list.Count == 0)
            {
                return;
            }

            do
            {
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTagged(list, tagName, slidingExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys with the associated tag name.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTaggedInterned(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count();
            if (count == 0)
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            var list = new List<KeyValuePair<string, byte[]>>(count);

            foreach (var cacheKeyAndObjectKvp in cacheKeysAndObjects)
            {
                byte[] bytes = null;
                try
                {
                    // Serialize
                    bytes = _binarySerializer.Serialize(cacheKeyAndObjectKvp.Value);
                    // Add to list
                    list.Add(new KeyValuePair<string, byte[]>(cacheKeyAndObjectKvp.Key, bytes));
                }
                catch
                {
                    // Log serialization error
                    _logger.Error("Serialization Error", "An object added via an AddOrUpdateMany call at cache key \"" + cacheKeyAndObjectKvp.Key + "\" could not be serialized");
                }
            }

            // Ensure we're doing something
            if (list.Count == 0)
            {
                return;
            }

            do
            {
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.AddOrUpdateTagged(list, tagName);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
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

            do
            {
                var client = DetermineClient(cacheKey);

                try
                {
                    client.Remove(cacheKey);
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
        /// Removes the objects associated to the given tag name from the cache.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        public void RemoveTagged(string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            do
            {
                // Cache all tagged items at the same server
                var client = DetermineClient(tagName);

                try
                {
                    client.RemoveTagged(tagName);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
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
    }
}

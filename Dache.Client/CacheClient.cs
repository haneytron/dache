using Dache.Client.Configuration;
using Dache.Client.Serialization;
using Dache.Core.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading;
using Dache.Client.Exceptions;

namespace Dache.Client
{
    /// <summary>
    /// The WCF client for cache host communication.
    /// </summary>
    public class CacheClient : ICacheClient
    {
        // The list of cache clients
        private readonly List<CacheHostBucket> _cacheHostLoadBalancingDistribution = new List<CacheHostBucket>(20);
        // The lock used to ensure state
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // The binary serializer
        private readonly IBinarySerializer _binarySerializer = null;
        // The logger
        private readonly ILogger _logger = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="binarySerializer">The custom binary serializer to use. Pass null to use the default serializer. NOTE: must be thread safe.</param>
        /// <param name="logger">The custom logger to use. Pass null to use the default logger. NOTE: must be thread safe.</param>
        public CacheClient(IBinarySerializer binarySerializer = null, ILogger logger = null)
        {
            // Assign custom serializer and logger
            _binarySerializer = binarySerializer ?? new BinarySerializer();
            _logger = logger ?? new EventViewerLogger("Cache Client", "Dache");

            // Get the cache hosts from configuration
            var cacheHosts = CacheClientConfigurationSection.Settings.CacheHosts;
            // Get the cache host reconnect interval from configuration
            var hostReconnectIntervalMilliseconds = CacheClientConfigurationSection.Settings.HostReconnectIntervalMilliseconds;

            // Sanitize
            if (cacheHosts == null)
            {
                throw new ConfigurationErrorsException("At least one cache host must be specified in your application's configuration.");
            }

            // Add the cache hosts to the cache client list
            foreach (CacheHostElement cacheHost in cacheHosts)
            {
                // Build the endpoint address
                var endpointAddressFormattedString = "net.tcp://{0}:{1}/Dache/CacheHost";
                var endpointAddress = new EndpointAddress(string.Format(endpointAddressFormattedString, cacheHost.Address, cacheHost.Port));
                // Build the net tcp binding
                var netTcpBinding = CreateNetTcpBinding();

                // Instantiate a cache host client container
                var clientContainer = new CommunicationClient(netTcpBinding, endpointAddress, hostReconnectIntervalMilliseconds);

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
        }

        /// <summary>
        /// Gets the object stored at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value or default for that type if the method returns false.</param>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <returns>True if successful, false otherwise.</returns>
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
        /// Gets the objects stored at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <returns>A list of the objects stored at the cache keys, or null if none were found.</returns>
        public IList<T> GetMany<T>(IEnumerable<string> cacheKeys)
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

            IList<byte[]> rawResults = null;

            do
            {
                // Use the first key's client
                var client = DetermineClient(cacheKeys.First());

                try
                {
                    rawResults = client.GetMany(cacheKeys, true);
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
                    _logger.Error("Serialization Error", "The object returned in a GetMany call at index " + i + " could not be deserialized to type " + typeof(T));
                }
            }

            return results;
        }

        /// <summary>
        /// Gets the objects stored at the given tag name from the cache.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <returns>A list of the objects stored at the tag name, or null if none were found.</returns>
        public IList<T> GetTagged<T>(string tagName)
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
                    rawResults = client.GetTagged(tagName, true);
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
        /// Adds or updates many objects in the cache at their given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        public void AddOrUpdateMany(ICollection<KeyValuePair<string, object>> cacheKeysAndObjects)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count;
            if (count == 0)
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
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
                    list.Add(new KeyValuePair<string,byte[]>(cacheKeyAndObjectKvp.Key, bytes));
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
                // Get client for first cache key
                var client = DetermineClient(list[0].Key);

                try
                {
                    client.AddOrUpdateMany(list);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at their given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateMany(ICollection<KeyValuePair<string, object>> cacheKeysAndObjects, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count;
            if (count == 0)
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
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
                // Get client for first cache key
                var client = DetermineClient(list[0].Key);

                try
                {
                    client.AddOrUpdateMany(list, absoluteExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at their given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateMany(ICollection<KeyValuePair<string, object>> cacheKeysAndObjects, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count;
            if (count == 0)
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndObjects");
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
                // Get client for first cache key
                var client = DetermineClient(list[0].Key);

                try
                {
                    client.AddOrUpdateMany(list, slidingExpiration);
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
                var client = DetermineClient(cacheKey);

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
                var client = DetermineClient(cacheKey);

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
                var client = DetermineClient(cacheKey);

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
        /// Adds or updates many objects in the cache at their given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateManyTagged(ICollection<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count;
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
                // Get client for first cache key
                var client = DetermineClient(list[0].Key);

                try
                {
                    client.AddOrUpdateManyTagged(list, tagName);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at their given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateManyTagged(ICollection<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count;
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
                // Get client for first cache key
                var client = DetermineClient(list[0].Key);

                try
                {
                    client.AddOrUpdateManyTagged(list, tagName, absoluteExpiration);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
                }
            } while (true);
        }

        /// <summary>
        /// Adds or updates many objects in the cache at their given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateManyTagged(ICollection<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndObjects");
            }
            var count = cacheKeysAndObjects.Count;
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
                // Get client for first cache key
                var client = DetermineClient(list[0].Key);

                try
                {
                    client.AddOrUpdateManyTagged(list, tagName, slidingExpiration);
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
        public void RemoveMany(IEnumerable<string> cacheKeys)
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
                // Get client for first cache key
                var client = DetermineClient(cacheKeys.First());

                try
                {
                    client.RemoveMany(cacheKeys);
                    break;
                }
                catch
                {
                    // Try a different cache host if this one could not be reached
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
                // Use the tag's client
                var client = DetermineClient(tagName);

                try
                {
                    client.RemoveTagged(tagName, true);
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
                if (cacheHostClient != null)
                {
                    _cacheHostLoadBalancingDistribution.Remove(cacheHostClient);
                    
                    // Calculate load balancing distribution
                    CalculateCacheHostLoadBalancingDistribution();
                }
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
                    // Multiply by C to add greater variation
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

        /// <summary>
        /// Creates a configured net tcp binding for communication.
        /// </summary>
        /// <returns>A configured net tcp binding.</returns>
        private NetTcpBinding CreateNetTcpBinding()
        {
            var netTcpBinding = new NetTcpBinding(SecurityMode.None, false)
            {
                CloseTimeout = TimeSpan.FromSeconds(15),
                OpenTimeout = TimeSpan.FromSeconds(15),
                SendTimeout = TimeSpan.FromSeconds(15),
                ReceiveTimeout = TimeSpan.MaxValue,
                Namespace = "http://schemas.getdache.net/cachehost",
                MaxBufferSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxConnections = 100000,
                ListenBacklog = 100000,
                TransferMode = System.ServiceModel.TransferMode.Buffered,
                ReliableSession = new OptionalReliableSession
                {
                    Enabled = false,
                },
            };

            // Set reader quotas
            netTcpBinding.ReaderQuotas.MaxDepth = 64;
            netTcpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;

            return netTcpBinding;
        }
    }
}

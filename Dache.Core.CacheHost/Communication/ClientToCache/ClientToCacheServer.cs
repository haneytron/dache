using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.ServiceModel;
using System.Text;
using Dache.Communication.ClientToCache;
using Dache.Core.CacheHost.Communication.CacheToCache;
using Dache.Core.CacheHost.Communication.CacheToManager;
using Dache.Core.CacheHost.State;
using Dache.Core.CacheHost.Storage;
using Dache.Core.DataStructures.Logging;
using Dache.Core.DataStructures.Routing;

namespace Dache.Core.CacheHost.Communication.ClientToCache
{
    /// <summary>
    /// The WCF server for client to cache communication.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false, MaxItemsInObjectGraph = int.MaxValue, Namespace = "http://schemas.getdache.net/cachehost")]
    public class ClientToCacheServer : IClientToCacheContract
    {
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

            // First figure out where the object at this cache key should exist
            ICacheToCacheClient cacheHostClient = null;
            if (!CacheHostManager.TryGetCacheHostClient(cacheKey, out cacheHostClient))
            {
                // Try to get locally
                return MemCacheContainer.Instance.Get(cacheKey);
            }

            // Go get it from the cache host
            try
            {
                return cacheHostClient.Get(cacheKey);
            }
            catch
            {
                // Get failed, return null so that the client will try for an add on an available server
                return null;
            }
        }

        /// <summary>
        /// Gets the serialized objects stored at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <param name="isClientRequest">Whether or not the request is from a client.</param>
        /// <returns>A list of the serialized objects.</returns>
        public IList<byte[]> GetMany(IEnumerable<string> cacheKeys, bool isClientRequest)
        {
            // Sanitize
            if (cacheKeys == null)
            {
                return null;
            }

            var result = new List<byte[]>(1000);

            // Iterate all cache keys
            foreach (var cacheKey in cacheKeys)
            {
                // Sanitize
                if (string.IsNullOrWhiteSpace(cacheKey))
                {
                    // Skip
                    continue;
                }

                // Try to get locally
                var getResult = MemCacheContainer.Instance.Get(cacheKey);
                if (getResult != null)
                {
                    result.Add(getResult);
                }
            }

            if (isClientRequest)
            {
                // Now get what all the other servers have
                foreach (var cacheHostClient in CacheHostManager.GetRegisteredCacheHosts())
                {
                    try
                    {
                        result.AddRange(cacheHostClient.GetMany(cacheKeys, false));
                    }
                    catch
                    {
                        // Get failed, so continue to another host
                    }
                }
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Gets all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="isClientRequest">Whether or not the request is from a client.</param>
        /// <returns>A list of the serialized objects.</returns>
        public IList<byte[]> GetTagged(string tagName, bool isClientRequest)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            // Compile a list of the serialized objects
            List<byte[]> result = new List<byte[]>(1000);

            // First add what we have locally
            var cacheKeys = RoutingTableContainer.Instance.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    result.Add(MemCacheContainer.Instance.Get(cacheKey));
                }
            }

            if (isClientRequest)
            {
                // Now get what all the other servers have
                foreach (var cacheHostClient in CacheHostManager.GetRegisteredCacheHosts())
                {
                    try
                    {
                        result.AddRange(cacheHostClient.GetTagged(tagName, false));
                    }
                    catch
                    {
                        // Get failed, so continue to another host
                    }
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
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy();

            // First figure out where the object at this cache key should exist
            ICacheToCacheClient cacheHostClient = null;
            if (!CacheHostManager.TryGetCacheHostClient(cacheKey, out cacheHostClient))
            {
                // Should exist locally
                MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
                return;
            }

            // Go add it to the cache host
            try
            {
                cacheHostClient.AddOrUpdate(cacheKey, serializedObject);
            }
            catch
            {
                // Store the object locally
                MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

                return;
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = absoluteExpiration
            };

            // First figure out where the object at this cache key should exist
            ICacheToCacheClient cacheHostClient = null;
            if (!CacheHostManager.TryGetCacheHostClient(cacheKey, out cacheHostClient))
            {
                // Should exist locally
                MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
                return;
            }

            // Go add it to the cache host
            try
            {
                cacheHostClient.AddOrUpdate(cacheKey, serializedObject, absoluteExpiration);
            }
            catch
            {
                // Store the object locally
                MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                SlidingExpiration = slidingExpiration
            };

            // First figure out where the object at this cache key should exist
            ICacheToCacheClient cacheHostClient = null;
            if (!CacheHostManager.TryGetCacheHostClient(cacheKey, out cacheHostClient))
            {
                // Should exist locally
                MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
                return;
            }

            // Go add it to the cache host
            try
            {
                cacheHostClient.AddOrUpdate(cacheKey, serializedObject, slidingExpiration);
            }
            catch
            {
                // Store the object locally
                MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

                return;
            }
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
        /// Adds or updates a serialized object in the cache at the given cache key and associates it with the given tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(string cacheKey, byte[] serializedObject, string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy();

            // Store the serialized object locally
            // We don't load balance tagged items so that there's a better chance that they'll end up on the same cache host
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

            // Add to the local routing table
            RoutingTableContainer.Instance.AddOrUpdate(cacheKey, tagName);
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
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = absoluteExpiration
            };

            // Store the serialized object locally
            // We don't load balance tagged items so that there's a better chance that they'll end up on the same cache host
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

            // Add to the local routing table
            RoutingTableContainer.Instance.AddOrUpdate(cacheKey, tagName);
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
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                SlidingExpiration = slidingExpiration
            };

            // Store the serialized object locally
            // We don't load balance tagged items so that there's a better chance that they'll end up on the same cache host
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

            // Add to the local routing table
            RoutingTableContainer.Instance.AddOrUpdate(cacheKey, tagName);
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
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName, slidingExpiration);
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

            // First figure out where the object at this cache key should exist
            ICacheToCacheClient cacheHostClient = null;
            if (!CacheHostManager.TryGetCacheHostClient(cacheKey, out cacheHostClient))
            {
                // Should exist locally
                MemCacheContainer.Instance.Remove(cacheKey);
                return;
            }

            // Go remove it from the cache host
            try
            {
                cacheHostClient.Remove(cacheKey);
            }
            catch
            {
                // The cache host will receive the command as soon as possible since the remove will enqueue it for execution once reconnection happens
            }
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
        /// <param name="isClientRequest">Whether or not the request is from a client.</param>
        public void RemoveTagged(string tagName, bool isClientRequest)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            // First remove what we have locally
            var cacheKeys = RoutingTableContainer.Instance.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    MemCacheContainer.Instance.Remove(cacheKey);
                }
            }

            if (isClientRequest)
            {
                // Now remove at all other cache hosts
                foreach (var cacheHostClient in CacheHostManager.GetRegisteredCacheHosts())
                {
                    try
                    {
                        cacheHostClient.RemoveTagged(tagName, false);
                    }
                    catch
                    {
                        // Remove failed, so continue to another host
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.ServiceModel;
using Dache.Communication;
using Dache.CacheHost.Storage;
using Dache.Core.Routing;

namespace Dache.CacheHost.Communication
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
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy();

            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
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

            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
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
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKey, serializedObject);
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy();

            // Store the serialized object locally
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

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
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }
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

            // Store the serialized object locally
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

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
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                return;
            }
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

            // Store the serialized object locally
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);

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
    }
}

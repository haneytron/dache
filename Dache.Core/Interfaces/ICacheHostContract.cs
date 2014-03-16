using System;
using System.Collections.Generic;

namespace Dache.Core.Communication
{
    /// <summary>
    /// Represents the communication contract between a cache client and a cache host.
    /// </summary>
    public interface ICacheHostContract
    {
        /// <summary>
        /// Gets the serialized objects stored at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the serialized objects.</returns>
        List<byte[]> Get(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Gets all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <returns>A list of the serialized objects.</returns>
        List<byte[]> GetTagged(IEnumerable<string> tagNames);

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects);

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, DateTimeOffset absoluteExpiration);

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, TimeSpan slidingExpiration);

        /// <summary>
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        void AddOrUpdateInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects);

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName);

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, DateTimeOffset absoluteExpiration);

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration);

        /// <summary>
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        void AddOrUpdateTaggedInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName);

        /// <summary>
        /// Removes the serialized objects at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        void Remove(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Removes all serialized objects associated with the given tag names and optionally with keys matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        void RemoveTagged(IEnumerable<string> tagNames, string pattern = "*");

        /// <summary>
        /// Gets all cache keys, optionally matching the provided pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        List<byte[]> GetCacheKeys(string pattern = "*");

        /// <summary>
        /// Gets all cache keys associated with the given tag names and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        List<byte[]> GetCacheKeysTagged(IEnumerable<string> tagNames, string pattern = "*");

        /// <summary>
        /// Clears the cache.
        /// </summary>
        void Clear();
    }
}

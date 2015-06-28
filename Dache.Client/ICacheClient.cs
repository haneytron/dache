using System;
using System.Collections.Generic;

namespace Dache.Client
{
    /// <summary>
    /// Represents a cache client.
    /// </summary>
    public interface ICacheClient
    {
        /// <summary>
        /// Gets the object stored at the given cache key from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value or default for that type if the method returns false.</param>
        /// <returns>true if successful, false otherwise.</returns>
        bool TryGet<T>(string cacheKey, out T value);

        /// <summary>
        /// Gets the objects stored at the given cache keys from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the objects stored at the cache keys, or null if none were found.</returns>
        List<T> Get<T>(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Gets the objects stored at the given tag name from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="tagName">The tag name.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>A list of the objects stored at the tag name, or null if none were found.</returns>
        List<T> GetTagged<T>(string tagName, string pattern = "*");

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
        void AddOrUpdate(string cacheKey, object value, string tagName = null, DateTimeOffset? absoluteExpiration = null, TimeSpan? slidingExpiration = null, bool notifyRemoved = false, bool isInterned = false);

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
        void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName = null, DateTimeOffset? absoluteExpiration = null, TimeSpan? slidingExpiration = null, bool notifyRemoved = false, bool isInterned = false);

        /// <summary>
        /// Removes the object at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        void Remove(string cacheKey);

        /// <summary>
        /// Removes the objects at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        void Remove(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Removes all serialized objects associated with the given tag name and optionally with keys matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        void RemoveTagged(string tagName, string pattern = "*");

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
        List<string> GetCacheKeys(string pattern = "*");

        /// <summary>
        /// Gets all cache keys associated with the given tag name and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        List<string> GetCacheKeysTagged(string tagName, string pattern = "*");

        /// <summary>
        /// Gets all cache keys associated with the given tag names and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        List<string> GetCacheKeysTagged(IEnumerable<string> tagNames, string pattern = "*");

        /// <summary>
        /// Clears the cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Shuts down the connection. Call this when unloading an app domain to gracefully exit.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Event that fires when the cache client is disconnected from a cache host.
        /// </summary>
        event EventHandler HostDisconnected;

        /// <summary>
        /// Event that fires when the cache client is successfully reconnected to a disconnected cache host.
        /// </summary>
        event EventHandler HostReconnected;

        /// <summary>
        /// Event that fires when a cached item has expired out of the cache.
        /// </summary>
        event EventHandler<CacheItemExpiredArgs> CacheItemExpired;
    }
}

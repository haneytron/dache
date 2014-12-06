using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace $rootnamespace$
{
    /// <summary>
    /// A cache provider.
    /// </summary>
    public class CacheProvider
    {
        // The static reference to the Dache cache client
        private static readonly CacheClient _cacheClient = new CacheClient();

        /// <summary>
        /// Adds or updates an item in the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The object to add to the cache.</param>
        public void AddOrUpdate<T>(string key, object value)
        {
            _cacheClient.AddOrUpdate(key, value);
        }

        /// <summary>
        /// Attempts to get an item from the cache.
        /// </summary>
        /// <typeparam name="T">The type of the object in the cache.</typeparam>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The object retrieved from the cache, or null if none found.</param>
        /// <returns>true if successful, false if an object was not found.</returns>
        public bool TryGet<T>(string key, out T value)
        {
            return _cacheClient.TryGet<T>(key, out value);
        }

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        public void Remove(string key)
        {
            _cacheClient.Remove(key);
        }

        /// <summary>
        /// Removes everything from the cache.
        /// </summary>
        public void ClearCache()
        {
            _cacheClient.Clear();
        }
    }
}

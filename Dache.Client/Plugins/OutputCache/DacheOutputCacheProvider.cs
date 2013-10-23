using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Caching;

namespace Dache.Client.Plugins.OutputCache
{
    /// <summary>
    /// The Dache output cache provider.
    /// </summary>
    public class DacheOutputCacheProvider : OutputCacheProvider
    {
        // The cache key
        private const string _cacheKey = "__DacheCustomOutputCaching_CacheKey:{0}";
        // The cache client
        private static readonly ICacheClient _cacheClient = new CacheClient();

        /// <summary>
        /// Inserts the specified entry into the output cache. 
        /// </summary>
        /// <param name="key">A unique identifier for entry.</param>
        /// <param name="entry">The content to add to the output cache.</param>
        /// <param name="utcExpiry">The time and date on which the cached entry expires.</param>
        /// <returns>A reference to the specified entry.</returns>
        public override object Add(string key, object entry, DateTime utcExpiry)
        {
            var cacheKey = string.Format(_cacheKey, key);

            object value = null;
            if (_cacheClient.TryGet<object>(cacheKey, out value))
            {
                return value;
            }

            _cacheClient.AddOrUpdate(cacheKey, entry, utcExpiry);
            return entry;
        }
        
        /// <summary>
        /// Returns a reference to the specified entry in the output cache.
        /// </summary>
        /// <param name="key">A unique identifier for a cached entry in the output cache.</param>
        /// <returns>The specified entry from the cache, or null if the specified entry is not in the cache.</returns>
        public override object Get(string key)
        {
            var cacheKey = string.Format(_cacheKey, key);

            object value = null;
            _cacheClient.TryGet<object>(cacheKey, out value);
            return value;
        }

        /// <summary>
        /// Removes the specified entry from the output cache.
        /// </summary>
        /// <param name="key">The unique identifier for the entry to remove from the output cache.</param>
        public override void Remove(string key)
        {
            var cacheKey = string.Format(_cacheKey, key);

            _cacheClient.Remove(cacheKey);
        }
        
        /// <summary>
        /// Inserts the specified entry into the output cache, overwriting the entry if it is already cached.
        /// </summary>
        /// <param name="key">A unique identifier for entry.</param>
        /// <param name="entry">The content to add to the output cache.</param>
        /// <param name="utcExpiry">The time and date on which the cached entry expires.</param>
        public override void Set(string key, object entry, DateTime utcExpiry)
        {
            var cacheKey = string.Format(_cacheKey, key);

            _cacheClient.AddOrUpdate(cacheKey, entry, utcExpiry);
        }
    }
}

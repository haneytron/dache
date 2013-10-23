using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;

namespace Dache.Client.Plugins.OutputCache
{
    /// <summary>
    /// The Dache output cache provider for MVC child action caching.
    /// TODO: make more generic, so that we also have a MemoryCache provider
    /// </summary>
    public class DacheMvcChildActionCache : MemoryCache
    {
        // The cache key
        private const string _cacheKey = "__DacheCustomMvcChildActionOutputCaching_CacheKey:{0}";
        // The cache client
        private readonly ICacheClient _cacheClient = new CacheClient();

        /// <summary>
        /// The constructor.
        /// </summary>
        public DacheMvcChildActionCache() 
            : base("Dache MVC Child Action Cache")
        {
        
        }
        
        /// <summary>
        /// Inserts a cache entry into the cache, overwriting any existing cache entry.
        /// </summary>
        /// <param name="key">A unique identifier for the cache entry.</param>
        /// <param name="value">The object to insert.</param>
        /// <param name="absoluteExpiration">The fixed date and time at which the cache entry will expire.</param>
        /// <param name="regionName">Ignored.</param>
        /// <returns>true if insertion succeeded, false otherwise.</returns>
        public override bool Add(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
        {
            var cacheKey = string.Format(_cacheKey, key);

            _cacheClient.AddOrUpdate(cacheKey, value, absoluteExpiration);
            
            return true;
        }
        
        /// <summary>
        /// Gets the specified cache entry from the cache as an object.
        /// </summary>
        /// <param name="key">A unique identifier for the cache entry to get.</param>
        /// <param name="regionName">Ignored.</param>
        /// <returns>The cache entry that is identified by key.</returns>
        public override object Get(string key, string regionName = null)
        {
            var cacheKey = string.Format(_cacheKey, key);

            object value = null;
            _cacheClient.TryGet<object>(cacheKey, out value);
            return value;
        }
    }
}

using System;
using System.Runtime.Caching;

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
        private readonly ICacheClient _cacheClient = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheClient">The cache client.</param>
        public DacheMvcChildActionCache(ICacheClient cacheClient) 
            : base("Dache MVC Child Action Cache")
        {
            // Sanitize
            if (cacheClient == null)
            {
                throw new ArgumentNullException("cacheClient");
            }

            _cacheClient = cacheClient;
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
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "key");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

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
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "key");
            }

            var cacheKey = string.Format(_cacheKey, key);

            object value = null;
            _cacheClient.TryGet<object>(cacheKey, out value);
            return value;
        }
    }
}

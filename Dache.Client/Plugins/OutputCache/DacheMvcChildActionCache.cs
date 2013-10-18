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
        
        public override bool Add(string key, object value, DateTimeOffset absoluteExpiration, string regionName = null)
        {
            var cacheKey = string.Format(_cacheKey, key);

            _cacheClient.AddOrUpdate(cacheKey, value, absoluteExpiration);
            
            return true;
        }
        
        public override object Get(string key, string regionName = null)
        {
            var cacheKey = string.Format(_cacheKey, key);

            object value = null;
            _cacheClient.TryGet<object>(cacheKey, out value);
            return value;
        }
    }
}

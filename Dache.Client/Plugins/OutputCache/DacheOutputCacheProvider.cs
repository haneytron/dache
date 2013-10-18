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
        private readonly ICacheClient _cacheClient = new CacheClient();

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
        
        public override object Get(string key)
        {
            var cacheKey = string.Format(_cacheKey, key);

            object value = null;
            _cacheClient.TryGet<object>(cacheKey, out value);
            return value;
        }
        public override void Remove(string key)
        {
            var cacheKey = string.Format(_cacheKey, key);

            _cacheClient.Remove(cacheKey);
        }
        
        public override void Set(string key, object entry, DateTime utcExpiry)
        {
            var cacheKey = string.Format(_cacheKey, key);

            _cacheClient.AddOrUpdate(cacheKey, entry, utcExpiry);
        }
    }
}

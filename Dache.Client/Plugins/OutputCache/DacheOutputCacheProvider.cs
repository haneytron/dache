using Dache.Client.Configuration;
using Dache.Client.Serialization;
using Dache.Core.Logging;
using System;
using System.Diagnostics;
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
        private static readonly ICacheClient _cacheClient = null;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static DacheOutputCacheProvider()
        {
            // Use the user provided settings
            var cacheClientConfig = CacheClientConfigurationSection.Settings;

            if (cacheClientConfig == null) throw new InvalidOperationException("You cannot use the Dache output cache provider without supplying Dache configuration in your web or app config file");

            // TODO: the below sucks. Improve it.

            // Clone to protect from mutated state
            var cacheClientConfigClone = (CacheClientConfigurationSection)cacheClientConfig.Clone();
            // Use binary serializer
            cacheClientConfigClone.CustomSerializer.Type = typeof(BinarySerializer).AssemblyQualifiedName;
            // Use Debug logger
            cacheClientConfigClone.CustomLogger.Type = typeof(DebugLogger).AssemblyQualifiedName;
            _cacheClient = new CacheClient(cacheClientConfigClone);
        }

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

            if (utcExpiry == DateTime.MaxValue)
            {
                _cacheClient.AddOrUpdate(cacheKey, entry);
            }
            else
            {
                _cacheClient.AddOrUpdate(cacheKey, entry, absoluteExpiration: utcExpiry);
            }

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

            if (utcExpiry == DateTime.MaxValue)
            {
                _cacheClient.AddOrUpdate(cacheKey, entry);
            }
            else
            {
                _cacheClient.AddOrUpdate(cacheKey, entry, absoluteExpiration: utcExpiry);
            }
        }
    }
}

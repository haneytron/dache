using System;
using System.Collections.Specialized;
using System.Runtime.Caching;
using Dache.CacheHost.Performance;

namespace Dache.CacheHost.Storage
{
    /// <summary>
    /// Encapsulates a memory cache that can store byte arrays. This type is thread safe.
    /// </summary>
    public class MemCache : IDisposable
    {
        // The underlying memory cache
        private readonly MemoryCache _memoryCache = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheName">The name of the cache.</param>
        /// <param name="cacheConfig">The cache configuration.</param>
        public MemCache(string cacheName, NameValueCollection cacheConfig = null)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheName))
            {
                throw new ArgumentNullException("cacheName");
            }

            _memoryCache = new MemoryCache(cacheName, cacheConfig);
        }

        /// <summary>
        /// Inserts or updates a byte array to the cache at the given key with the specified or default cache item policy.
        /// </summary>
        /// <param name="key">The key of the byte array. Null is not supported.</param>
        /// <param name="value">The byte array. Null is not supported.</param>
        /// <param name="cacheItemPolicy">The cache item policy.</param>
        public void Add(string key, byte[] value, CacheItemPolicy cacheItemPolicy)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("key is null, empty, or white space");
            }
            if (value == null)
            {
                // MemoryCache does not support null values
                throw new ArgumentNullException("value");
            }
            if (cacheItemPolicy == null)
            {
                throw new ArgumentNullException("cacheItemPolicy");
            }

            // Add the custom change monitor
            //var loadBalancingChangeMonitor = new LoadBalancingChangeMonitor(key, loadBalancingMethod);
            //LoadBalanceRequired += loadBalancingChangeMonitor.LoadBalancingRequired;
            //cacheItemPolicy.ChangeMonitors.Add(loadBalancingChangeMonitor);

            _memoryCache.Set(key, value, cacheItemPolicy);

            // Increment the Add counter
            CustomPerformanceCounterManagerContainer.Instance.AddsPerSecond.RawValue++;
            // Increment the Total counter
            CustomPerformanceCounterManagerContainer.Instance.TotalRequestsPerSecond.RawValue++;
        }

        /// <summary>
        /// Gets a byte array from the cache.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <returns>The byte array if found, otherwise null.</returns>
        public byte[] Get(string key)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            // Increment the Get counter
            CustomPerformanceCounterManagerContainer.Instance.GetsPerSecond.RawValue++;
            // Increment the Total counter
            CustomPerformanceCounterManagerContainer.Instance.TotalRequestsPerSecond.RawValue++;

            return _memoryCache.Get(key) as byte[];
        }

        /// <summary>
        /// Removes a byte array from the cache.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <returns>The byte array if the key was found in the cache, otherwise null.</returns>
        public byte[] Remove(string key)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            // Increment the Remove counter
            CustomPerformanceCounterManagerContainer.Instance.RemovesPerSecond.RawValue++;
            // Increment the Total counter
            CustomPerformanceCounterManagerContainer.Instance.TotalRequestsPerSecond.RawValue++;

            return _memoryCache.Remove(key) as byte[];
        }

        /// <summary>
        /// Returns the total number of cache entries in the cache.
        /// </summary>
        /// <returns>The cache entry count.</returns>
        public long GetCount()
        {
            return _memoryCache.GetCount();
        }

        /// <summary>
        /// Triggered when load balancing is required.
        /// </summary>
        public event EventHandler LoadBalanceRequired;

        /// <summary>
        /// Triggers the load balancing required event.
        /// </summary>
        public void OnLoadBalanceRequired()
        {
            var loadBalanceRequired = LoadBalanceRequired;
            if (loadBalanceRequired != null)
            {
                loadBalanceRequired(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Called when disposed.
        /// </summary>
        public void Dispose()
        {
            _memoryCache.Dispose();
        }
    }
}

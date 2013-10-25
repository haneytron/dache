using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Caching;
using System.Threading;
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
        // The dictionary that serves as an intern set, with the key being the cache key and the value being a hash code to a potentially shared object
        private readonly IDictionary<string, string> _internDictionary = null;
        // The intern dictionary lock
        private readonly ReaderWriterLockSlim _internDictionaryLock = new ReaderWriterLockSlim();

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
            _internDictionary = new Dictionary<string, string>(100);
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

            // Intern this key
            var hashKey = CalculateHash(value);
            _internDictionaryLock.EnterWriteLock();
            try
            {
                // Intern the value
                _internDictionary[key] = hashKey;
            }
            finally
            {
                _internDictionaryLock.ExitWriteLock();
            }

            // Now possibly add to MemoryCache
            if (!_memoryCache.Contains(hashKey))
            {
                _memoryCache[hashKey] = value;
            }

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

            string hashKey = null;
            _internDictionaryLock.EnterReadLock();
            try
            {
                if (!_internDictionary.TryGetValue(key, out hashKey))
                {
                    // Doesn't exist
                    return null;
                }
            }
            finally
            {
                _internDictionaryLock.ExitReadLock();
            }

            return _memoryCache.Get(hashKey) as byte[];
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

            string hashKey = null;
            // Delete this interned key
            _internDictionaryLock.EnterUpgradeableReadLock();
            try
            {
                if (!_internDictionary.TryGetValue(key, out hashKey))
                {
                    // Nothing to do
                    return null;
                }

                // Got it, remove it
                _internDictionaryLock.EnterWriteLock();
                try
                {
                    _internDictionary.Remove(key);
                }
                finally
                {
                    _internDictionaryLock.ExitWriteLock();
                }
            }
            finally
            {
                _internDictionaryLock.ExitUpgradeableReadLock();
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
            return _internDictionary.Count;
        }

        /// <summary>
        /// Called when disposed.
        /// </summary>
        public void Dispose()
        {
            _memoryCache.Dispose();
        }

        /// <summary>
        /// Calculates a unique hash for a byte array.
        /// </summary>
        /// <param name="value">The byte array.</param>
        /// <returns>The resulting hash value.</returns>
        private string CalculateHash(byte[] value)
        {
            int result = 13 * value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                result = (17 * result) + value[i];
            }
            // TODO: should I intern this?
            return string.Intern(result.ToString());
        }
    }
}

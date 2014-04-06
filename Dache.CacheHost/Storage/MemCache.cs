﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading;
using Dache.Core.Performance;

namespace Dache.CacheHost.Storage
{
    /// <summary>
    /// Encapsulates a memory cache that can store byte arrays. This type is thread safe.
    /// </summary>
    public class MemCache : IMemCache
    {
        // The underlying memory cache
        private MemoryCache _memoryCache = null;
        // The memory cache lock
        private readonly ReaderWriterLockSlim _memoryCacheLock = new ReaderWriterLockSlim();
        // The custom performance counter manager
        private readonly ICustomPerformanceCounterManager _customPerformanceCounterManager = null;
        // The dictionary that serves as an intern set, with the key being the cache key and the value being a hash code to a potentially shared object
        private readonly IDictionary<string, string> _internDictionary = null;
        // The dictionary that serves as an intern reference count, with the key being the hash code and the value being the number of references to the object
        private readonly IDictionary<string, int> _internReferenceDictionary = null;
        // The interned object cache item policy
        private static readonly CacheItemPolicy _internCacheItemPolicy = new CacheItemPolicy { Priority = CacheItemPriority.NotRemovable };
        // The intern dictionary lock
        private readonly ReaderWriterLockSlim _internDictionaryLock = new ReaderWriterLockSlim();
        // The cache name
        private string _cacheName;
        // The cache configuration
        private NameValueCollection _cacheConfig;

        // The method that sets the MemoryCache._stats._lastTrimGen2Count
        private readonly Action<MemoryCache, int> _setMemoryCacheLastTrimGen2CountFunc = null;
        // The timer that calls the last trim gen 2 count function
        private readonly Timer _setMemoryCacheLastTrimGen2CountTimer = null;
        // The integer incremented as the gen 2 count value
        private int _lastTrimGen2Count = 0;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheName">The name of the cache.</param>
        /// <param name="physicalMemoryLimitPercentage">The cache memory limit, as a percentage of the total system memory.</param>
        /// <param name="customPerformanceCounterManager">The custom performance counter manager.</param>
        public MemCache(string cacheName, int physicalMemoryLimitPercentage, ICustomPerformanceCounterManager customPerformanceCounterManager)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheName))
            {
                throw new ArgumentNullException("cacheName");
            }
            if (physicalMemoryLimitPercentage <= 0)
            {
                throw new ArgumentException("cannot be <= 0", "physicalMemoryLimitPercentage");
            }
            if (customPerformanceCounterManager == null)
            {
                throw new ArgumentNullException("customPerformanceCounterManager");
            }

            _cacheName = cacheName;
            _cacheConfig = new NameValueCollection();
            _cacheConfig.Add("pollingInterval", "00:00:05");
            _cacheConfig.Add("cacheMemoryLimitMegabytes", "0");
            //_cacheConfig.Add("physicalMemoryLimitPercentage", physicalMemoryLimitPercentage.ToString(CultureInfo.InvariantCulture));
            _cacheConfig.Add("physicalMemoryLimitPercentage", "1");

            _memoryCache = new MemoryCache(_cacheName, _cacheConfig);
            _internDictionary = new Dictionary<string, string>(100);
            _internReferenceDictionary = new Dictionary<string, int>(100);

            _customPerformanceCounterManager = customPerformanceCounterManager;

            // Use lambda expressions to create a set method for MemoryCache._stats._lastTrimGen2Count to circumvent poor functionality of MemoryCache
            // The default MemoryCache does not check for memory pressure except after a Gen 2 Garbage Collection. We want to do this more often than that.
            // So this method allows us to reset the field the MemoryCacheStatistics object uses periodically to a new value, to force the trim to be checked.

            // Define the types
            var memoryCacheType = _memoryCache.GetType();
            var memoryCacheStatisticsType = memoryCacheType.Assembly.GetType("System.Runtime.Caching.MemoryCacheStatistics", true);

            // Define the _stats field on MemoryCache
            var statsField = memoryCacheType.GetField("_stats", BindingFlags.Instance | BindingFlags.NonPublic);
            // Define the _lastTrimGen2Count field on MemoryCacheStatistics
            var lastTrimGen2CountField = memoryCacheStatisticsType.GetField("_lastTrimGen2Count", BindingFlags.Instance | BindingFlags.NonPublic);

            // Define the parameters to the method
            var targetExpression = Expression.Parameter(memoryCacheType, "target");
            var valueExpression = Expression.Parameter(typeof(int), "value");

            // Create the field expressions
            var statsFieldExpression = Expression.Field(targetExpression, statsField);
            var lastTrimGen2CountFieldExpression = Expression.Field(statsFieldExpression, lastTrimGen2CountField);

            // Create the field value assignment expression
            var fieldValueAssignmentExpression = Expression.Assign(lastTrimGen2CountFieldExpression, valueExpression);

            // Compile to function
            _setMemoryCacheLastTrimGen2CountFunc = Expression.Lambda<Action<MemoryCache, int>>(fieldValueAssignmentExpression, targetExpression, valueExpression).Compile();

            // Configure the timer to fire at half of the polling interval - this ensures the value is different when the MemoryCache code looks at it via polling
            _setMemoryCacheLastTrimGen2CountTimer = new Timer((state) =>
            {
                _setMemoryCacheLastTrimGen2CountFunc(_memoryCache, _lastTrimGen2Count++ % 10);
            }, null, 2500, 2500);
        }

        /// <summary>
        /// Inserts or updates a byte array in the cache at the given key with the specified cache item policy.
        /// </summary>
        /// <param name="key">The key of the byte array. Null is not supported.</param>
        /// <param name="value">The byte array. Null is not supported.</param>
        /// <param name="cacheItemPolicy">The cache item policy.</param>
        public void Add(string key, byte[] value, CacheItemPolicy cacheItemPolicy)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "key");
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

            _memoryCacheLock.EnterReadLock();
            try
            {
                // Add to the cache
                _memoryCache.Set(key, value, cacheItemPolicy);
            }
            finally
            {
                _memoryCacheLock.ExitReadLock();
            }

            // Increment the Add counter
            _customPerformanceCounterManager.AddsPerSecond.RawValue++;
            // Increment the Total counter
            _customPerformanceCounterManager.TotalRequestsPerSecond.RawValue++;
        }

        /// <summary>
        /// Inserts or updates an interned byte array in the cache at the given key. 
        /// Interned values cannot expire or be evicted unless removed manually.
        /// </summary>
        /// <param name="key">The key of the byte array. Null is not supported.</param>
        /// <param name="value">The byte array. Null is not supported.</param>
        public void AddInterned(string key, byte[] value)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "key");
            }
            if (value == null)
            {
                // MemoryCache does not support null values
                throw new ArgumentNullException("value");
            }

            // Intern this key
            var hashKey = CalculateHash(value);
            int referenceCount = 0;

            _internDictionaryLock.EnterWriteLock();
            try
            {
                // Get the old hash key if it exists
                if (_internDictionary.ContainsKey(key))
                {
                    var oldHashKey = _internDictionary[key];
                    // Do a remove to decrement intern reference count
                    referenceCount = --_internReferenceDictionary[oldHashKey];

                    // Check if reference is dead
                    if (referenceCount == 0)
                    {
                        _memoryCacheLock.EnterReadLock();
                        try
                        {
                            // Remove actual old object
                            _memoryCache.Remove(oldHashKey);
                        }
                        finally
                        {
                            _memoryCacheLock.ExitReadLock();
                        }
                    }
                }
                // Intern the value
                _internDictionary[key] = hashKey;
                if (!_internReferenceDictionary.TryGetValue(hashKey, out referenceCount))
                {
                    _internReferenceDictionary[hashKey] = referenceCount;
                }

                _internReferenceDictionary[hashKey]++;
            }
            finally
            {
                _internDictionaryLock.ExitWriteLock();
            }

            // Now possibly add to MemoryCache
            if (!_memoryCache.Contains(hashKey))
            {
                _memoryCacheLock.EnterReadLock();
                try
                {
                    _memoryCache.Set(hashKey, value, _internCacheItemPolicy);
                }
                finally
                {
                    _memoryCacheLock.ExitReadLock();
                }
            }

            // Increment the Add counter
            _customPerformanceCounterManager.AddsPerSecond.RawValue++;
            // Increment the Total counter
            _customPerformanceCounterManager.TotalRequestsPerSecond.RawValue++;
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
            _customPerformanceCounterManager.GetsPerSecond.RawValue++;
            // Increment the Total counter
            _customPerformanceCounterManager.TotalRequestsPerSecond.RawValue++;


            // Check for interned
            string hashKey = null;
            _internDictionaryLock.EnterReadLock();
            try
            {
                if (!_internDictionary.TryGetValue(key, out hashKey))
                {
                    // Not interned
                    _memoryCacheLock.EnterReadLock();
                    try
                    {
                        return _memoryCache.Get(key) as byte[];
                    }
                    finally
                    {
                        _memoryCacheLock.ExitReadLock();
                    }
                }
            }
            finally
            {
                _internDictionaryLock.ExitReadLock();
            }

            _memoryCacheLock.EnterReadLock();
            try
            {
                return _memoryCache.Get(hashKey) as byte[];
            }
            finally
            {
                _memoryCacheLock.ExitReadLock();
            }
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
            _customPerformanceCounterManager.RemovesPerSecond.RawValue++;
            // Increment the Total counter
            _customPerformanceCounterManager.TotalRequestsPerSecond.RawValue++;

            string hashKey = null;
            int referenceCount = 0;
            // Delete this interned key
            _internDictionaryLock.EnterReadLock();
            try
            {
                if (!_internDictionary.TryGetValue(key, out hashKey))
                {
                    // Not interned, do normal work
                    _memoryCacheLock.EnterReadLock();
                    try
                    {
                        return _memoryCache.Remove(key) as byte[];
                    }
                    finally
                    {
                        _memoryCacheLock.ExitReadLock();
                    }
                }
            }
            finally
            {
                _internDictionaryLock.ExitReadLock();
            }

            // Is interned, remove it
            _internDictionaryLock.EnterWriteLock();
            try
            {
                // Double lock check to ensure still interned
                if (_internDictionary.TryGetValue(key, out hashKey))
                {
                    _internDictionary.Remove(key);
                    referenceCount = --_internReferenceDictionary[hashKey];

                    // Check if reference is dead
                    if (referenceCount == 0)
                    {
                        // Remove actual object
                        _memoryCacheLock.EnterReadLock();
                        try
                        {
                            return _memoryCache.Remove(hashKey) as byte[];
                        }
                        finally
                        {
                            _memoryCacheLock.ExitReadLock();
                        }
                    }
                }
            }
            finally
            {
                _internDictionaryLock.EnterWriteLock();
            }

            // Interned object still exists, so fake the removal return of the object
            _memoryCacheLock.EnterReadLock();
            try
            {
                return _memoryCache.Get(hashKey) as byte[];
            }
            finally
            {
                _memoryCacheLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public void Clear()
        {
            _memoryCacheLock.EnterWriteLock();
            try
            {
                var oldCache = _memoryCache;
                _memoryCache = new MemoryCache(_cacheName, _cacheConfig);
                oldCache.Dispose();
            }
            finally
            {
                _memoryCacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets all the keys in the cache. WARNING: this is likely a very expensive operation for large caches. 
        /// </summary>
        public IList<string> Keys(string pattern)
        {
            Regex regex = pattern == "*" ? null : new Regex(pattern, RegexOptions.IgnoreCase);

            _memoryCacheLock.EnterWriteLock();
            try
            {
                // Lock ensures single thread, so parallelize to improve response time
                return _memoryCache.AsParallel().Where(kvp => regex == null ? true : regex.IsMatch(kvp.Key)).Select(kvp => kvp.Key).ToList();
            }
            finally
            {
                _memoryCacheLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Total number of objects in the cache.
        /// </summary>
        public long Count
        {
            get
            {
                // The total interned keys minus the actual hash keys plus the regular count
                return _internDictionary.Count - _internReferenceDictionary.Count + _memoryCache.GetCount();
            }
        }

        /// <summary>
        /// Gets the amount of memory on the computer, in megabytes, that can be used by the cache.
        /// </summary>
        public long MemoryLimit
        {
            get
            {
                return _memoryCache.CacheMemoryLimit;
            }
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
            
            // Return custom intern key
            return string.Format("__InternedCacheKey_{0}", result);
        }
    }
}

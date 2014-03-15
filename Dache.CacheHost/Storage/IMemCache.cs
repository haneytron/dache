using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace Dache.CacheHost.Storage
{
    /// <summary>
    /// Represents a memory cache that can store byte arrays.
    /// </summary>
    public interface IMemCache : IDisposable
    {
        /// <summary>
        /// Inserts or updates a byte array in the cache at the given key with the specified cache item policy.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <param name="value">The byte array.</param>
        /// <param name="cacheItemPolicy">The cache item policy.</param>
        void Add(string key, byte[] value, CacheItemPolicy cacheItemPolicy);

        /// <summary>
        /// Inserts or updates an interned byte array in the cache at the given key. 
        /// Interned values cannot expire or be evicted unless removed manually.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <param name="value">The byte array.</param>
        void AddInterned(string key, byte[] value);

        /// <summary>
        /// Gets a byte array from the cache.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <returns>The byte array if found, otherwise null.</returns>
        byte[] Get(string key);

        /// <summary>
        /// Removes a byte array from the cache.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <returns>The byte array if the key was found in the cache, otherwise null.</returns>
        byte[] Remove(string key);

        /// <summary>
        /// Clears the cache
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets all the keys in the cache matching the provided pattern. WARNING: this is likely a very expensive operation for large caches. 
        /// </summary>
        /// <param name="pattern">The search pattern (regex)</param>
        /// <returns>The list of keys matching the provided pattern</returns>
        IList<string> Keys(string pattern);

        /// <summary>
        /// Total number of objects in the cache.
        /// </summary>
        long Count { get; }

        /// <summary>
        /// Gets the amount of memory on the computer, in megabytes, that can be used by the cache.
        /// </summary>
        long MemoryLimit { get; }
    }
}

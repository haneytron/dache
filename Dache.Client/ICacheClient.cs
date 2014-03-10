﻿using System;
using System.Collections.Generic;

namespace Dache.Client
{
    /// <summary>
    /// Represents a cache client.
    /// </summary>
    public interface ICacheClient
    {
        /// <summary>
        /// Gets the object stored at the given cache key from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value or default for that type if the method returns false.</param>
        /// <returns>true if successful, false otherwise.</returns>
        bool TryGet<T>(string cacheKey, out T value);

        /// <summary>
        /// Gets the object stored at the given cache key from the local cache. If it is not found in the local 
        /// cache, the object is retrieved remotely and cached locally for subsequent local lookups.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value or default for that type if the method returns false.</param>
        /// <returns>true if successful, false otherwise.</returns>
        bool TryGetLocal<T>(string cacheKey, out T value);

        /// <summary>
        /// Gets the objects stored at the given cache keys from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the objects stored at the cache keys, or null if none were found.</returns>
        List<T> Get<T>(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Gets the objects stored at the given cache keys from the local cache. If they are not found in the local 
        /// cache, the objects are retrieved remotely and cached locally for subsequent local lookups.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the objects stored at the cache keys, or null if none were found.</returns>
        List<T> GetLocal<T>(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Gets the objects stored at the given tag name from the cache.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="tagName">The tag name.</param>
        /// <returns>A list of the objects stored at the tag name, or null if none were found.</returns>
        List<T> GetTagged<T>(string tagName);

        /// <summary>
        /// Gets the objects stored at the given tag name from the local cache. If they are not found in the local 
        /// cache, the objects are retrieved remotely and cached locally for subsequent local lookups.
        /// </summary>
        /// <typeparam name="T">The expected type.</typeparam>
        /// <param name="tagName">The tag name.</param>
        /// <returns>A list of the objects stored at the tag name, or null if none were found.</returns>
        List<T> GetTaggedLocal<T>(string tagName);

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        void AddOrUpdate(string cacheKey, object value);

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        void AddOrUpdate(string cacheKey, object value, DateTimeOffset absoluteExpiration);

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        void AddOrUpdate(string cacheKey, object value, TimeSpan slidingExpiration);

        /// <summary>
        /// Adds or updates an interned object in the cache at the given cache key.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        void AddOrUpdateInterned(string cacheKey, object value);

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects);

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, DateTimeOffset absoluteExpiration);

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        void AddOrUpdate(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, TimeSpan slidingExpiration);

        /// <summary>
        /// Adds or updates the interned objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        void AddOrUpdateInterned(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects);

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key with the associated tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        void AddOrUpdateTagged(string cacheKey, object value, string tagName);

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key with the associated tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        void AddOrUpdateTagged(string cacheKey, object value, string tagName, DateTimeOffset absoluteExpiration);

        /// <summary>
        /// Adds or updates an object in the cache at the given cache key with the associated tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        void AddOrUpdateTagged(string cacheKey, object value, string tagName, TimeSpan slidingExpiration);

        /// <summary>
        /// Adds or updates the interned object in the cache at the given cache key with the associated tag name.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <param name="tagName">The tag name.</param>
        void AddOrUpdateTaggedInterned(string cacheKey, object value, string tagName);

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName);

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName, DateTimeOffset absoluteExpiration);

        /// <summary>
        /// Adds or updates many objects in the cache at thr given cache keys with the associated tag name.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName, TimeSpan slidingExpiration);

        /// <summary>
        /// Adds or updates many objects in the cache at the given cache keys with the associated tag name.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndObjects">The cache keys and their associated objects.</param>
        /// <param name="tagName">The tag name.</param>
        void AddOrUpdateTaggedInterned(IEnumerable<KeyValuePair<string, object>> cacheKeysAndObjects, string tagName);

        /// <summary>
        /// Removes the object at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        void Remove(string cacheKey);

        /// <summary>
        /// Removes the objects at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        void Remove(IEnumerable<string> cacheKeys);

        /// <summary>
        /// Removes the objects associated to the given tag name from the cache.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        void RemoveTagged(string tagName);

        /// <summary>
        /// Event that fires when the cache client is disconnected from a cache host.
        /// </summary>
        event EventHandler HostDisconnected;

        /// <summary>
        /// Event that fires when the cache client is successfully reconnected to a disconnected cache host.
        /// </summary>
        event EventHandler HostReconnected;
    }
}

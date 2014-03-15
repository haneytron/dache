using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace Dache.CacheHost.Storage
{
    /// <summary>
    /// Encapsulates a memory cache that can compress and store byte arrays. This type is thread safe.
    /// </summary>
    public class GZipMemCache : IMemCache
    {
        // The underlying mem cache
        private MemCache _memCache = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="GZipMemCache"/> class.
        /// </summary>
        /// <param name="memCache">The memory cache.</param>
        public GZipMemCache(MemCache memCache)
        {
            _memCache = memCache;
        }

        /// <summary>
        /// Inserts or updates a byte array in the cache at the given key with the specified cache item policy.
        /// </summary>
        /// <param name="key">The key of the byte array. Null is not supported.</param>
        /// <param name="value">The byte array. Null is not supported.</param>
        /// <param name="cacheItemPolicy">The cache item policy.</param>
        /// <remarks>
        /// Passed byte array will be compressed by using <see cref="GZipStream"/>.
        /// </remarks>
        public void Add(string key, byte[] value, CacheItemPolicy cacheItemPolicy)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "key");
            }
            if (value == null)
            {
                // GZipMemCache does not support null values
                throw new ArgumentNullException("value");
            }
            if (cacheItemPolicy == null)
            {
                throw new ArgumentNullException("cacheItemPolicy");
            }

            _memCache.Add(key, Compress(value).Result, cacheItemPolicy);
        }

        /// <summary>
        /// Inserts or updates an interned byte array in the cache at the given key.
        /// Interned values cannot expire or be evicted unless removed manually.
        /// </summary>
        /// <param name="key">The key of the byte array. Null is not supported.</param>
        /// <param name="value">The byte array. Null is not supported.</param>
        /// <remarks>
        /// Passed byte array will be compressed by using <see cref="GZipStream"/>.
        /// </remarks>
        public void AddInterned(string key, byte[] value)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "key");
            }
            if (value == null)
            {
                // GZipMemCache does not support null values
                throw new ArgumentNullException("value");
            }

            _memCache.AddInterned(key, Compress(value).Result);
        }

        /// <summary>
        /// Gets a byte array from the cache.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <returns>
        /// The byte array if found, otherwise null.
        /// </returns>
        /// <remarks>
        /// Return byte array will be decompressed via <see cref="GZipStream"/> before being returned.
        /// </remarks>
        public byte[] Get(string key)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            return Decompress(_memCache.Get(key)).Result;
        }

        /// <summary>
        /// Removes a byte array from the cache.
        /// </summary>
        /// <param name="key">The key of the byte array.</param>
        /// <returns>The byte array if the key was found in the cache, otherwise null.</returns>
        public byte[] Remove(string key)
        {
            return _memCache.Remove(key);
        }

        /// <summary>
        /// Clears the cache
        /// </summary>
        public void Clear()
        {
            _memCache.Clear();
        }

        /// <summary>
        /// Gets all the keys in the cache. WARNING: this is likely a very expensive operation for large caches. 
        /// </summary>
        public IList<string> Keys(string pattern)
        {
            return _memCache.Keys(pattern);
        }

        /// <summary>
        /// Total number of objects in the cache.
        /// </summary>
        public long Count
        {
            get
            { 
                return _memCache.Count;
            }
        }

        /// <summary>
        /// Gets the amount of memory on the computer, in megabytes, that can be used by the cache.
        /// </summary>
        public long MemoryLimit
        {
            get
            { 
                return _memCache.MemoryLimit;
            }
        }

        /// <summary>
        /// Called when disposed.
        /// </summary>
        public void Dispose()
        {
            _memCache.Dispose();
        }

        /// <summary>
        /// Compresses the specified byte array.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>Compressed byte array.</returns>
        /// <remarks>
        /// <see cref="GZipStream"/> is used for compression tasks.
        /// </remarks>
        protected async Task<byte[]> Compress(byte[] value)
        {
            // Sanitize
            if (value == null)
            {
                throw new ArgumentNullException("value", "Can't compress null value.");
            }

            using (MemoryStream originalStream = new MemoryStream(value))
            {
                using (MemoryStream compressedStream = new MemoryStream())
                {
                    using (GZipStream compressionStream = new GZipStream(compressedStream, CompressionMode.Compress))
                    {
                        await originalStream.CopyToAsync(compressionStream);
                    }

                    return compressedStream.ToArray();
                }
            }
        }

        /// <summary>
        /// Decompresses the specified byte array.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>Decompressed byte array.</returns>
        /// <remarks>
        /// <see cref="GZipStream"/> is used for decompression tasks.
        /// </remarks>
        protected async Task<byte[]> Decompress(byte[] value)
        {
            // Sanitize
            if (value == null)
            {
                return null;
            }

            using (MemoryStream originalStream = new MemoryStream(value))
            {
                using (MemoryStream decompressedStream = new MemoryStream())
                {
                    using (GZipStream decompressionStream = new GZipStream(originalStream, CompressionMode.Decompress))
                    {
                        await decompressionStream.CopyToAsync(decompressedStream);
                    }

                    return decompressedStream.ToArray();
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.CacheHost.Persistence
{
    /// <summary>
    /// Contains a persisted item.
    /// </summary>
    [Serializable]
    public sealed class PersistedItem
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The cached item.</param>
        /// <param name="isInterned">Whether or not the cached item is interned.</param>
        /// <param name="tagName">The tag name. Optional.</param>
        public PersistedItem(string cacheKey, byte[] value, bool isInterned, string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            CacheKey = cacheKey;
            Value = value;
            IsInterned = isInterned;
            TagName = tagName;
        }

        /// <summary>
        /// The cache key.
        /// </summary>
        public string CacheKey { get; private set; }

        /// <summary>
        /// The cached item.
        /// </summary>
        public byte[] Value { get; private set; }

        /// <summary>
        /// Whether or not the cached item is interned.
        /// </summary>
        public bool IsInterned { get; private set; }

        /// <summary>
        /// The tag name. Optional.
        /// </summary>
        public string TagName { get; private set; }
    }
}

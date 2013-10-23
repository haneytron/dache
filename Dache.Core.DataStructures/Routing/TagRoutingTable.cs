using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

namespace Dache.Core.DataStructures.Routing
{
    /// <summary>
    /// The routing table that indicates which tags contain which cache keys. Thread safe.
    /// </summary>
    public class TagRoutingTable
    {
        // The tagged cache keys: key is tag name, value is cache keys
        private IDictionary<string, HashSet<string>> _taggedCacheKeys = new Dictionary<string, HashSet<string>>(1000);
        // The cache keys and their associated tags: key is cache key, value is tag name
        private IDictionary<string, string> _cacheKeyTags = new Dictionary<string, string>(1000);

        // The lock used to ensure thread safety
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // The singleton instance
        private static readonly TagRoutingTable _instance = new TagRoutingTable();

        /// <summary>
        /// Private constructor for singleton.
        /// </summary>
        private TagRoutingTable()
        {

        }

        /// <summary>
        /// The singleton instance.
        /// </summary>
        public static TagRoutingTable Instance
        {
            get
            {
                return _instance;
            }
        }

        /// <summary>
        /// Adds a cache key with an associated tag name to the routing table if the key does not already exist, or 
        /// updates a cache key with an associated tag name in the routing table if the cache key already exists.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdate(string cacheKey, string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            // Intern
            cacheKey = string.Intern(cacheKey);
            tagName = string.Intern(tagName);

            _lock.EnterWriteLock();
            try
            {
                // First remove from tag dictionaries if the key already existed as tagged
                RemoveFromTagDictionaries(cacheKey);

                // Add to dictionaries
                AddToTagDictionaries(cacheKey, tagName);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes the given cache key from the routing table.
        /// </summary>
        /// <param name="cacheKey">the cache key.</param>
        public void Remove(string cacheKey)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            _lock.EnterWriteLock();
            try
            {
                // First remove from tag dictionaries if the key already existed as tagged
                RemoveFromTagDictionaries(cacheKey);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets the tagged cache keys for a given tag.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <returns>The tagged cache keys, or null if none were found.</returns>
        public IList<string> GetTaggedCacheKeys(string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            _lock.EnterReadLock();
            try
            {
                // Get the cache keys
                HashSet<string> cacheKeys = null;
                if (!_taggedCacheKeys.TryGetValue(tagName, out cacheKeys))
                {
                    return null;
                }
                // Return a copy of the cache keys
                return new List<string>(cacheKeys);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Removes a cache key from the tag dictionaries if it exists.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        private void RemoveFromTagDictionaries(string cacheKey)
        {
            HashSet<string> taggedCacheKeys = null;
            string oldTagName = null;
            if (_cacheKeyTags.TryGetValue(cacheKey, out oldTagName))
            {
                if (_taggedCacheKeys.TryGetValue(oldTagName, out taggedCacheKeys))
                {
                    taggedCacheKeys.Remove(cacheKey);
                }
            }

            _taggedCacheKeys.Remove(cacheKey);
        }

        /// <summary>
        /// Adds a given cache key and tag to the tag dictionaries.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="tagName">The tag name.</param>
        private void AddToTagDictionaries(string cacheKey, string tagName)
        {
            HashSet<string> taggedCacheKeys = null;
            _cacheKeyTags[cacheKey] = tagName;
            if (!_taggedCacheKeys.TryGetValue(tagName, out taggedCacheKeys))
            {
                taggedCacheKeys = new HashSet<string>();
                _taggedCacheKeys.Add(tagName, taggedCacheKeys);
            }

            taggedCacheKeys.Add(cacheKey);
        }
    }
}

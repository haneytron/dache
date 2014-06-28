using System.Collections.Generic;

namespace Dache.CacheHost.Routing
{
    /// <summary>
    /// Represents a routing table that indicates which tags contain which cache keys.
    /// </summary>
    public interface ITagRoutingTable
    {
        /// <summary>
        /// Adds a cache key with an associated tag name to the routing table if the key does not already exist, or 
        /// updates a cache key with an associated tag name in the routing table if the cache key already exists.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="tagName">The tag name.</param>
        void AddOrUpdate(string cacheKey, string tagName);

        /// <summary>
        /// Removes the given cache key from the routing table.
        /// </summary>
        /// <param name="cacheKey">the cache key.</param>
        void Remove(string cacheKey);

        /// <summary>
        /// Gets the tagged cache keys for a given tag.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <returns>The tagged cache keys, or null if none were found.</returns>
        IList<string> GetTaggedCacheKeys(string tagName);

        /// <summary>
        /// Gets the tagged cache keys for a given tag and matching the given search pattern.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="pattern">The search pattern. If no pattern is provided, default '*' (all) is used.</param>
        /// <returns>The tagged cache keys, or null if none were found.</returns>
        IList<string> GetTaggedCacheKeys(string tagName, string pattern);
    }
}

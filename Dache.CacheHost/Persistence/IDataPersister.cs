using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.CacheHost.Persistence
{
    /// <summary>
    /// Persists data to another medium.
    /// </summary>
    public interface IDataPersister
    {
        /// <summary>
        /// Persists a byte array to the medium.
        /// </summary>
        /// <param name="persistedItem">The persisted item.</param>
        void Persist(PersistedItem persistedItem);

        /// <summary>
        /// Attempts to load a byte array from the medium.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="persistedItem">The persisted item.</param>
        /// <returns>true if successful, false otherwise.</returns>
        bool TryLoad(string cacheKey, out PersistedItem persistedItem);

        /// <summary>
        /// Loads all items from the medium, performing the specific function on each persisted item.
        /// </summary>
        /// <param name="persistedItemFunc">The function to perform on each persisted item.</param>
        void LoadAll(Action<PersistedItem> persistedItemFunc);

        /// <summary>
        /// Removes a persisted item from the medium.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        void Remove(string cacheKey);
    }
}

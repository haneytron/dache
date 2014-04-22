using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dache.CacheHost.Persistence
{
    /// <summary>
    /// Persists data to disk.
    /// </summary>
    public class DiskDataPersister : IDataPersister
    {
        // The persistence directory
        private readonly string _persistenceDirectory = null;

        // The modulus lock dictionary
        private readonly Dictionary<int, ReaderWriterLockSlim> _modulusLockDictionary = new Dictionary<int, ReaderWriterLockSlim>();

        /// <summary>
        /// The constructor.
        /// </summary>
        public DiskDataPersister()
        {
            _persistenceDirectory = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "_data");
            Directory.CreateDirectory(_persistenceDirectory);

            for (int i = 0; i < 50; i++)
            {
                _modulusLockDictionary.Add(i, new ReaderWriterLockSlim());
            }
        }

        /// <summary>
        /// Persists a byte array to the medium.
        /// </summary>
        /// <param name="persistedItem">The persisted item.</param>
        public void Persist(PersistedItem persistedItem)
        {
            // Sanitize
            if (persistedItem == null)
            {
                throw new ArgumentNullException("persistedItem");
            }

            var cacheKeyHashCode = CalculateHash(persistedItem.CacheKey);
            var filePath = Path.Combine(_persistenceDirectory, string.Format("{0}-{1}", cacheKeyHashCode, CalculateHash(persistedItem.Value)));

            // Get the lock
            var cacheKeyLock = _modulusLockDictionary[cacheKeyHashCode % _modulusLockDictionary.Count];
            cacheKeyLock.EnterWriteLock();
            try
            {
                File.WriteAllBytes(filePath, Serialize(persistedItem));
            }
            finally
            {
                cacheKeyLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempts to load a byte array from the medium.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="persistedItem">The persisted item.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public bool TryLoad(string cacheKey, out PersistedItem persistedItem)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            // Find the right file
            var cacheKeyHashCode = CalculateHash(cacheKey);
            foreach (var fileName in Directory.GetFiles(_persistenceDirectory, string.Format("{0}-*", cacheKeyHashCode)))
            {
                byte[] file = null;

                // Get the lock
                var cacheKeyLock = _modulusLockDictionary[cacheKeyHashCode % _modulusLockDictionary.Count];
                cacheKeyLock.EnterReadLock();
                try
                {
                    file = File.ReadAllBytes(fileName);    
                }
                finally
                {
                    cacheKeyLock.ExitReadLock();
                }
                
                var deserialized = (PersistedItem)Deserialize(file);
                if (string.Equals(cacheKey, deserialized.CacheKey))
                {
                    persistedItem = deserialized;
                    return true;
                }
            }

            // Failed
            persistedItem = null;
            return false;
        }

        /// <summary>
        /// Loads all items from the medium, performing the specific function on each persisted item.
        /// </summary>
        /// <param name="persistedItemFunc">The function to perform on each persisted item.</param>
        public void LoadAll(Action<PersistedItem> persistedItemFunc)
        {
            // Iterate all files
            Parallel.ForEach(Directory.GetFiles(_persistenceDirectory), fileName =>
            {
                byte[] file = null;

                // Get the lock
                var cacheKeyHashCode = CalculateHash(fileName.Substring(0, fileName.IndexOf('-')));
                var cacheKeyLock = _modulusLockDictionary[cacheKeyHashCode % _modulusLockDictionary.Count];
                cacheKeyLock.EnterReadLock();
                try
                {
                    file = File.ReadAllBytes(fileName);
                }
                finally
                {
                    cacheKeyLock.ExitReadLock();
                }

                var deserialized = (PersistedItem)Deserialize(file);
                // Perform the function
                persistedItemFunc(deserialized);
            });
        }

        /// <summary>
        /// Removes a persisted item from the medium.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public void Remove(string cacheKey)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            // Find the right file
            foreach (var fileName in Directory.GetFiles(_persistenceDirectory, string.Format("{0}-*", cacheKey.GetHashCode())))
            {
                byte[] file = null;

                // Get the lock
                var cacheKeyHashCode = CalculateHash(fileName.Substring(0, fileName.IndexOf('-')));
                var cacheKeyLock = _modulusLockDictionary[cacheKeyHashCode % _modulusLockDictionary.Count];
                cacheKeyLock.EnterReadLock();
                try
                {
                    file = File.ReadAllBytes(fileName);
                }
                finally
                {
                    cacheKeyLock.ExitReadLock();
                }

                var deserialized = (PersistedItem)Deserialize(file);
                if (string.Equals(cacheKey, deserialized.CacheKey))
                {
                    cacheKeyLock.EnterWriteLock();
                    try
                    {
                        File.Delete(fileName);
                    }
                    finally
                    {
                        cacheKeyLock.ExitWriteLock();
                    }
                }
            }
        }

        /// <summary>
        /// Serializes an object to byte array.
        /// </summary>
        /// <param name="value">The object.</param>
        /// <returns>A byte array of the serialized object, or null if the object was null.</returns>
        private static byte[] Serialize(object value)
        {
            // Sanitize
            if (value == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(memoryStream, value);

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a byte array into an object.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <returns>An object, or null if the byte array was null.</returns>
        private static object Deserialize(byte[] bytes)
        {
            // Sanitize
            if (bytes == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream(bytes))
            {
                return new BinaryFormatter().Deserialize(memoryStream);
            }
        }

        /// <summary>
        /// Calculates a unique hash for a byte array.
        /// </summary>
        /// <param name="value">The byte array.</param>
        /// <returns>The resulting hash value.</returns>
        private static int CalculateHash(byte[] value)
        {
            int result = 13 * value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                result = (17 * result) + value[i];
            }

            // Return custom hash key
            return result;
        }

        /// <summary>
        /// Calculates a unique hash for a string.
        /// </summary>
        /// <param name="value">The string.</param>
        /// <returns>The resulting hash value.</returns>
        private static int CalculateHash(string value)
        {
            return CalculateHash(Encoding.UTF8.GetBytes(value));
        }
    }
}

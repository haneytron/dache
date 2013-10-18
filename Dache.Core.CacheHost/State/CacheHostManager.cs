using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using Dache.Core.CacheHost.Communication.CacheToCache;
using Dache.Core.CacheHost.Storage;

namespace Dache.Core.CacheHost.State
{
    /// <summary>
    /// The manager of known cache hosts. Thread safe.
    /// </summary>
    internal static class CacheHostManager
    {
        // The cache host collection. Key is host address, value is cache host communication object and cached object count
        private static IDictionary<string, ICacheToCacheClient> _cacheHostCollection = new Dictionary<string, ICacheToCacheClient>(20);
        // The cache host load balancing distribution
        private static List<CacheHostBucket> _cacheHostLoadBalancingDistribution = new List<CacheHostBucket>(20);
        // The lock used to ensure thread safety
        private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        /// <summary>
        /// Adds a cache host to the manager. Will overwrite prior entries at the same host address.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="cacheHostClient">The cache host client.</param>
        /// <param name="registrantIndex">The registrant index.</param>
        /// <param name="highestRegistrantIndex">The highest registrant index.</param>
        public static void Register(string hostAddress, ICacheToCacheClient cacheHostClient, int registrantIndex, int highestRegistrantIndex)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "hostAddress");
            }
            if (cacheHostClient == null)
            {
                throw new ArgumentNullException("cacheHostClient");
            }

            _lock.EnterWriteLock();
            try
            {
                // Add cache host client to collection via indexer syntax to allow overwriting
                _cacheHostCollection[hostAddress] = cacheHostClient;

                // If the registrant number is no equal to the count of the load balancing distribution, add local
                if (registrantIndex != _cacheHostLoadBalancingDistribution.Count)
                {
                    // Add local cache host client
                    _cacheHostLoadBalancingDistribution.Add(new CacheHostBucket
                    {
                        CacheHost = null
                    });
                }

                // Add cache host client to load balancing distribution at the registrant number
                _cacheHostLoadBalancingDistribution.Add(new CacheHostBucket
                {
                    CacheHost = cacheHostClient
                });

                // Finally, if the registrant index is one less than the highest registrant index, but the load balancing distribution count is equal, add local
                if (registrantIndex == highestRegistrantIndex - 1 && _cacheHostLoadBalancingDistribution.Count == highestRegistrantIndex)
                {
                    // Add local cache host client
                    _cacheHostLoadBalancingDistribution.Add(new CacheHostBucket
                    {
                        CacheHost = null
                    });
                }

                // Now calculate the cache host load balancing distribution
                CalculateCacheHostLoadBalancingDistribution();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempt to retrieve a cache host client by its address.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="cacheHostClient">The cache host client.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public static bool TryGetCacheHostClient(string cacheKey, out ICacheToCacheClient cacheHostClient)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                cacheHostClient = null;
                return false;
            }

            // If no other cache hosts are registered, done
            if (_cacheHostCollection.Count == 0)
            {
                cacheHostClient = null;
                return false;
            }

            // Attempt to retrieve cache host information
            _lock.EnterReadLock();
            try
            {
                // Compute Hash Code
                var hashCode = ComputeHashCode(cacheKey);

                // Figure out which host we want
                var index = BinarySearch(hashCode);

                // Get the cache host client
                cacheHostClient = _cacheHostLoadBalancingDistribution[index].CacheHost;

                // No host applies, must be local host
                if (cacheHostClient == null)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                cacheHostClient = null;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Removes the cache host from the manager.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        public static void Deregister(string hostAddress)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "hostAddress");
            }

            // Get the cache host client
            ICacheToCacheClient cacheHostClient = null;
            if (!TryGetCacheHostClient(hostAddress, out cacheHostClient))
            {
                // Not valid, skip it
                return;
            }

            _lock.EnterWriteLock();
            try
            {
                _cacheHostCollection.Remove(hostAddress);
            }
            finally
            {
                // Now recalculate the cache host load balancing distribution
                CalculateCacheHostLoadBalancingDistribution();

                _lock.ExitWriteLock();
                // Close the cache host client connection
                cacheHostClient.CloseConnection();
            }
        }

        /// <summary>
        /// Removes all cache hosts from the manager.
        /// </summary>
        public static void DeregisterAll()
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var cacheHostClientKvp in _cacheHostCollection)
                {
                    // Close the cache host client connection
                    cacheHostClientKvp.Value.CloseConnection();
                }
                
                // Clear all cache hosts
                _cacheHostCollection.Clear();
            }
            finally
            {
                // Now recalculate the cache host load balancing distribution
                CalculateCacheHostLoadBalancingDistribution();

                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets all registered cache hosts.
        /// </summary>
        /// <returns>The registered cache hosts.</returns>
        public static IList<ICacheToCacheClient> GetRegisteredCacheHosts()
        {
            _lock.EnterReadLock();
            try
            {
                return new List<ICacheToCacheClient>(_cacheHostCollection.Values);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Calculates the cache host load balancing distribution by considering the average object count across all hosts as well as the cached object count 
        /// at each of the hosts.
        /// </summary>
        private static void CalculateCacheHostLoadBalancingDistribution()
        {
            // Get the number of cache hosts (add 1 to include local)
            var registeredCacheHostCount = _cacheHostCollection.Count + 1;

            int x = 0;
            // Iterate all cache hosts in the load balancing distribution
            for (int i = 0; i < _cacheHostLoadBalancingDistribution.Count; i++)
            {
                // Get the current cache host bucket
                var cacheHostBucket = _cacheHostLoadBalancingDistribution[i];

                // Determine current range
                int currentMinimum = (int)((long)(x * uint.MaxValue) / registeredCacheHostCount) - int.MaxValue - 1;
                // If not first iteration
                if (x > 0)
                {
                    // Add 1
                    currentMinimum++;
                }
                x++;
                int currentMaximum = (int)((long)(x * uint.MaxValue) / registeredCacheHostCount) - int.MaxValue - 1;

                // Update values
                cacheHostBucket.MinValue = currentMinimum;
                cacheHostBucket.MaxValue = currentMaximum;
            }

            // Trigger a load balancing change
            if (registeredCacheHostCount > 1)
            {
                // TODO: needed?
                MemCacheContainer.Instance.OnLoadBalanceRequired();
            }
        }

        /// <summary>
        /// Computes an integer hash code for a cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>A hash code.</returns>
        private static int ComputeHashCode(string cacheKey)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in cacheKey)
                {
                    // Multiply by C to add greater variation
                    hash = (hash * 23 + c) * c;
                }
                return hash;
            }
        }

        /// <summary>
        /// Binary searches the cache host load balancing distribution for the index of the matching cache host.
        /// </summary>
        /// <param name="hashCode">The hash code.</param>
        /// <returns>A negative value if no cache host applies, otherwise the index of the cache host.</returns>
        private static int BinarySearch(int hashCode)
        {
            // Find the middle of the list, rounded down
            var middleIndex = _cacheHostLoadBalancingDistribution.Count / 2;
            // Do the binary search recursively
            return BinarySearchRecursive(hashCode, middleIndex);
        }

        /// <summary>
        /// Recursively binary searches the cache host load balancing distribution for the index of the matching cache host.
        /// </summary>
        /// <param name="hashCode">The hash code.</param>
        /// <param name="currentIndex">The current index.</param>
        /// <returns>A negative value if no cache host applies, otherwise the index of the cache host.</returns>
        private static int BinarySearchRecursive(int hashCode, int currentIndex)
        {
            var currentCacheHost = _cacheHostLoadBalancingDistribution[currentIndex];
            if (currentCacheHost.MinValue > hashCode)
            {
                // Go left
                return BinarySearchRecursive(hashCode, currentIndex / 2);
            }
            if (currentCacheHost.MaxValue < hashCode)
            {
                // Go right
                return BinarySearchRecursive(hashCode, (int)(currentIndex * 1.5));
            }

            // Otherwise check if we're all done
            if (currentCacheHost.MinValue <= hashCode && currentCacheHost.MaxValue >= hashCode)
            {
                return currentIndex;
            }

            // If we got here it doesn't exist, return the one's complement of where we are which will be negative
            return ~currentIndex;
        }

        /// <summary>
        /// Provides cache host and bucket range information
        /// </summary>
        private class CacheHostBucket
        {
            /// <summary>
            /// The cache host.
            /// </summary>
            public ICacheToCacheClient CacheHost { get; set; }

            /// <summary>
            /// The minimum value of the range.
            /// </summary>
            public int MinValue { get; set; }

            /// <summary>
            /// The maximum value of the range.
            /// </summary>
            public int MaxValue { get; set; }
        }
    }
}

using Dache.Communication.CacheToManager;
using Dache.Core.DataStructures.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Dache.Core.DataStructures.Performance;

namespace Dache.Core.CacheManager.State
{
    /// <summary>
    /// The manager of known cache hosts. Thread safe.
    /// </summary>
    internal static class CacheHostManager
    {
        // The cache host collection. Key is host address, value is cache host communication object and cached object count
        private static List<CacheHostInformation> _cacheHostCollection = new List<CacheHostInformation>(20);
        // The lock used to ensure thread safety
        private static readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        // The deregistration queue
        private static readonly Queue<IManagerToCacheCallbackContract> _deregistrationQueue = new Queue<IManagerToCacheCallbackContract>(10);
        // The deregistration timer
        private static readonly Timer _deregistrationTimer = null;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static CacheHostManager()
        {
            // Initialize the deregistration timer
            _deregistrationTimer = new Timer(DeregistrationThread, null, 5000, 5000);
        }

        /// <summary>
        /// Registers a cache host with the manager. Will overwrite prior entries at the same host address.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="performanceCounterInstanceName">The performance counter instance name.</param>
        /// <param name="cacheHostClient">The cache host client.</param>
        /// <param name="cachedObjectCount">The cached object count.</param>
        public static void Register(string hostAddress, string performanceCounterInstanceName, IManagerToCacheCallbackContract cacheHostClient, long cachedObjectCount)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "hostAddress");
            }
            if (string.IsNullOrWhiteSpace(performanceCounterInstanceName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "performanceCounterInstanceName");
            }
            if (cacheHostClient == null)
            {
                throw new ArgumentNullException("cacheHostClient");
            }
            if (cachedObjectCount < 0)
            {
                throw new ArgumentException("cannot be < 0", "cachedObjectCount");
            }

            _lock.EnterWriteLock();
            try
            {
                // Tell every other cache host to register this cache host, and this cache host to register all other cache hosts
                for (int i = 0; i < _cacheHostCollection.Count; i++)
                {
                    var cacheHostInformation = _cacheHostCollection[i];
                    try
                    {
                        // Have existing cache host register the new cache host
                        cacheHostInformation.CacheHostClient.RegisterHost(hostAddress, _cacheHostCollection.Count, _cacheHostCollection.Count);
                    }
                    catch
                    {
                        // Could not talk to the given cache host, needs to be deregistered
                        Deregister(cacheHostInformation.CacheHostClient);
                    }

                    // Have new cache host register existing cache host
                    cacheHostClient.RegisterHost(cacheHostInformation.HostAddress, i, _cacheHostCollection.Count);
                }

                // Get the performance counters
                var machineName = new Uri(hostAddress).Host;
                var customPerformanceCounterManager = new CustomPerformanceCounterManager(machineName, performanceCounterInstanceName);

                // Add cache host client to collection via indexer syntax to allow overwriting
                _cacheHostCollection.Add(new CacheHostInformation
                {
                    HostAddress = hostAddress,
                    CacheHostClient = cacheHostClient,
                    CachedObjectCount = cachedObjectCount,
                    CustomPerformanceCounterManager = customPerformanceCounterManager
                });
            }
            catch
            {
                // Could not talk to the newly registered cache host, needs to be deregistered
                Deregister(cacheHostClient);

                // Bubble it up
                throw;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Attempt to retrieve the cache host by its address.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="cacheHostClient">The cache host client.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public static bool TryGetCacheHostClient(string hostAddress, out IManagerToCacheCallbackContract cacheHostClient)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                cacheHostClient = null;
                return false;
            }

            // Attempt to retrieve cache host information
            CacheHostInformation cacheHostInformation;
            _lock.EnterReadLock();
            try
            {
                cacheHostInformation = _cacheHostCollection.FirstOrDefault(i => string.Equals(hostAddress, i.HostAddress));
                if (cacheHostInformation == null)
                {
                    // Failed
                    cacheHostClient = null;
                    return false;
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }

            // Succeeded
            cacheHostClient = cacheHostInformation.CacheHostClient;
            return true;
        }

        /// <summary>
        /// Removes the cache host from the manager.
        /// </summary>
        /// <param name="cacheHostClient">The cache host client.</param>
        public static void Deregister(IManagerToCacheCallbackContract cacheHostClient)
        {
            // Sanitize
            if (cacheHostClient == null)
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheHostClient");
            }

            // Lock the deregistration queue
            lock (_deregistrationQueue)
            {
                _deregistrationQueue.Enqueue(cacheHostClient);
            }
        }

        /// <summary>
        /// The thread that does deregistrations.
        /// </summary>
        /// <param name="state">The state. Ignored.</param>
        private static void DeregistrationThread(object state)
        {
            IManagerToCacheCallbackContract cacheHostClient = null;

            // Lock the deregistration queue
            lock (_deregistrationQueue)
            {
                if (_deregistrationQueue.Count == 0)
                {
                    // Nothing to do
                    return;
                }

                cacheHostClient = _deregistrationQueue.Dequeue();
            }

            _lock.EnterWriteLock();
            try
            {
                // Find the cache host
                if (!_cacheHostCollection.Any(i => i.CacheHostClient.Equals(cacheHostClient)))
                {
                    // Not found, so we're done
                    return;
                }

                // Found, so assign host address
                var cacheHostInformation = _cacheHostCollection.First(i => i.CacheHostClient.Equals(cacheHostClient));

                // Remove the cache host
                var result = _cacheHostCollection.Remove(cacheHostInformation);

                if (result)
                {
                    // Tell every other cache host to deregister this cache host
                    foreach (var otherCacheHostInformation in _cacheHostCollection)
                    {
                        // Have existing cache host deregister the cache host
                        try
                        {
                            otherCacheHostInformation.CacheHostClient.DeregisterHost(cacheHostInformation.HostAddress);
                        }
                        catch
                        {
                            // This cache host needs to be deregistered
                            Deregister(otherCacheHostInformation.CacheHostClient);
                        }
                    }

                    LoggerContainer.Instance.Info("Cache Host Client Deregistration", "Cache host at address " + cacheHostInformation.HostAddress + " has successfully deregistered.");
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates the performance counters of all hosts.
        /// </summary>
        public static void UpdatePerformanceCounters()
        {
            // Check if we even need to do anything
            if (_cacheHostCollection.Count == 0)
            {
                // Nope
                return;
            }

            _lock.EnterReadLock();
            try
            {
                // Iterate all cache hosts
                foreach (var cacheHostInformation in _cacheHostCollection)
                {
                    try
                    {
                        // Update performance counters
                        cacheHostInformation.CustomPerformanceCounterManager.UpdateAll();
                    }
                    catch
                    {
                        // Communication failed
                        LoggerContainer.Instance.Warn("Update Cached Object Counts", "Unable to read performance counters from cache host: " + cacheHostInformation.HostAddress);
                        // TODO: deregister in this case?
                    }
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets all cache host performance counters.
        /// </summary>
        /// <returns>The performance counters indexed at the key of cache host address.</returns>
        public static IList<KeyValuePair<string, PerformanceCounter[]>> GetPerformanceCounters()
        {
            var result = new List<KeyValuePair<string, PerformanceCounter[]>>(_cacheHostCollection.Count);

            // Check if we even need to do anything
            if (_cacheHostCollection.Count == 0)
            {
                // Nope
                return result;
            }

            _lock.EnterReadLock();
            try
            {
                // Iterate all cache hosts
                foreach (var cacheHostInformation in _cacheHostCollection)
                {
                    result.Add(new KeyValuePair<string,PerformanceCounter[]>(cacheHostInformation.HostAddress, cacheHostInformation.CustomPerformanceCounterManager.GetAll().ToArray()));
                }

                return result;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Contains a cache host and its cached object count.
        /// </summary>
        private class CacheHostInformation
        {
            /// <summary>
            /// The host address.
            /// </summary>
            public string HostAddress { get; set; }

            /// <summary>
            /// The cache host client.
            /// </summary>
            public IManagerToCacheCallbackContract CacheHostClient { get; set; }

            /// <summary>
            /// The cached object count.
            /// </summary>
            public long CachedObjectCount { get; set; }

            /// <summary>
            /// The custom performance counter manager.
            /// </summary>
            public CustomPerformanceCounterManager CustomPerformanceCounterManager { get; set; }
        }
    }
}

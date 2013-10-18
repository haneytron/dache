using Dache.Communication.CacheToManager;
using Dache.Core.CacheManager.State;
using Dache.Core.DataStructures.Logging;
using Dache.Core.DataStructures.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dache.Core.CacheManager.Communication
{
    /// <summary>
    /// The WCF server for cache to manager communication.
    /// </summary>
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false, MaxItemsInObjectGraph = int.MaxValue, Namespace = "http://schemas.getdache.net/cachemanager")]
    public class CacheToManagerServer : ICacheToManagerContract
    {
        // The lock that ensures atomic state management
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        /// <summary>
        /// Registers the cache instance with the cache manager.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="cachedObjectCount">The cached object count.</param>
        public void Register(string hostAddress, long cachedObjectCount)
        {
            LoggerContainer.Instance.Info("Cache Host Client Registration", "Cache host at address " + hostAddress + " is being registered.");

            // Register the host with callback contract
            var cacheHostClient = OperationContext.Current.GetCallbackChannel<IManagerToCacheCallbackContract>();
            var cacheHostAddress = new Uri(hostAddress);
            var cacheHostClientMachineName = string.Format("{0}_{1}", cacheHostAddress.Host, cacheHostAddress.Port);
            // Special case: replace "localhost" with "."
            if (string.Equals(cacheHostClientMachineName, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                // Set localhost to "."
                cacheHostClientMachineName = ".";
            }

            _lock.EnterWriteLock();
            try
            {
                CacheHostManager.Register(hostAddress, cacheHostClientMachineName, cacheHostClient, cachedObjectCount);
            }
            catch
            {
                LoggerContainer.Instance.Warn("Cache Host Client Registration", "Failed to register cache host at address " + hostAddress + ".");
                // Bubble up
                throw;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            // Hook into Closed/Faulted events to deregister the client if this client fails
            OperationContext.Current.Channel.Closed += ClientClosed;
            OperationContext.Current.Channel.Faulted += ClientFaulted;

            LoggerContainer.Instance.Info("Cache Host Client Registration", "Cache host at address " + hostAddress + " has successfully registered.");
        }

        private void ClientClosed(object sender, EventArgs e)
        {
            LoggerContainer.Instance.Info("Cache Host Client Deregistration", "Cache host has closed the connection, it will now be deregistered.");

            try
            {
                // Get cache host client that closed the connection
                var cacheHostClient = (IManagerToCacheCallbackContract)sender;

                // Deregister the client
                Deregister(cacheHostClient);
            }
            catch (Exception ex)
            {
                LoggerContainer.Instance.Error("Error occurred while handling client closed event.", ex);
            }
        }

        private void ClientFaulted(object sender, EventArgs e)
        {
            LoggerContainer.Instance.Warn("Cache Host Client Deregistration", "Cache host has unexpectedly closed the connection, it will now be deregistered.");

            try
            {
                // Get cache host client that closed the connection
                var cacheHostClient = (IManagerToCacheCallbackContract)sender;

                // Deregister the client
                Deregister(cacheHostClient);
            }
            catch (Exception ex)
            {
                LoggerContainer.Instance.Error("Error occurred while handling client faulted event.", ex);
            }
        }

        /// <summary>
        /// Deregisters a connected cache host client.
        /// </summary>
        /// <param name="cacheHostClient">The cache host client.</param>
        private void Deregister(IManagerToCacheCallbackContract cacheHostClient)
        {
            // Sanitize
            if (cacheHostClient == null)
            {
                throw new ArgumentNullException("cacheHostClient");
            }

            _lock.EnterWriteLock();
            try
            {
                CacheHostManager.Deregister(cacheHostClient);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Dache.Communication.CacheToManager;
using System.Threading;
using Dache.Core.CacheHost.State;
using Dache.Core.CacheHost.Storage;

namespace Dache.Core.CacheHost.Communication.CacheToManager
{
    /// <summary>
    /// The WCF client for cache to manager communication.
    /// </summary>
    public class CacheToManagerClient : ICacheManagerClient
    {
        // The WCF channel factory
        private readonly DuplexChannelFactory<ICacheToManagerContract> _channelFactory = null;
        // The WCF proxy
        private ICacheToManagerContract _proxy = null;
        // The WCF proxy communication object
        private ICommunicationObject _proxyComm = null;

        // The cache manager reconnect interval, in milliseconds
        private readonly int _managerReconnectIntervalMilliseconds = 0;

        // Whether or not the communication client is connected
        private volatile bool _isConnected = false;
        // The lock object used for reconnection
        private readonly object _reconnectLock = new object();
        // The timer used to reconnect to the server
        private readonly Timer _reconnectTimer = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="managerReconnectIntervalMilliseconds">The cache manager reconnect interval, in milliseconds.</param>
        public CacheToManagerClient(string hostAddress, int managerReconnectIntervalMilliseconds)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "hostAddress");
            }
            if (managerReconnectIntervalMilliseconds <= 0)
            {
                throw new ArgumentException("must be greater than 0", "managerReconnectIntervalMilliseconds");
            }

            // Build the endpoint address
            var endpointAddress = new EndpointAddress(hostAddress);
            // Build the net tcp binding
            var netTcpBinding = CreateNetTcpBinding();

            // Initialize the channel factory with the binding and endpoint address
            _channelFactory = new DuplexChannelFactory<ICacheToManagerContract>(new InstanceContext(new ManagerToCacheCallback()), netTcpBinding, endpointAddress);

            _managerReconnectIntervalMilliseconds = managerReconnectIntervalMilliseconds;

            // Initialize WCF
            _proxy = _channelFactory.CreateChannel();
            _proxyComm = _proxy as ICommunicationObject;
            // Register for disconnection events
            _proxyComm.Closed += DisconnectFromServer;
            _proxyComm.Faulted += DisconnectFromServer;

            // Set connected before opening to avoid a race
            _isConnected = true;

            // Initialize and configure the reconnect timer to never fire
            _reconnectTimer = new Timer(ReconnectToServer, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Registers the cache instance with the cache manager.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="cachedObjectCount">The cached object count.</param>
        public void Register(string hostAddress, long cachedObjectCount)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "hostAddress");
            }

            try
            {
                _proxy.Register(hostAddress, cachedObjectCount);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
            }
        }

        /// <summary>
        /// Closes the connection to the cache manager.
        /// </summary>
        public void CloseConnection()
        {
            // Lock to ensure atomic operations
            lock (_reconnectLock)
            {
                // Set the reconnect timer to never fire
                _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            // Disconnect WCF
            try
            {
                _proxyComm.Close();
            }
            catch
            {
                // Abort
                _proxyComm.Abort();
            }
        }

        /// <summary>
        /// Event that fires when the cache host is disconnected from the cache manager.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Event that fires when the cache host is successfully reconnected to the disconnected cache manager.
        /// </summary>
        public event EventHandler Reconnected;

        /// <summary>
        /// Makes the client enter the disconnected state.
        /// </summary>
        /// <param name="sender">The sender. Ignored.</param>
        /// <param name="e">The event args. Ignored.</param>
        private void DisconnectFromServer(object sender = null, EventArgs e = null)
        {
            // Check if already disconnected - ahead of the lock for speed
            if (!_isConnected)
            {
                // Do nothing
                return;
            }

            // Obtain the reconnect lock to ensure that we only even enter the disconnected state once per disconnect
            lock (_reconnectLock)
            {
                // Double check if already disconnected
                if (!_isConnected)
                {
                    // Do nothing
                    return;
                }

                // Deregister for disconnection events
                _proxyComm.Closed -= DisconnectFromServer;
                _proxyComm.Faulted -= DisconnectFromServer;

                // Fire the disconnected event
                var disconnected = Disconnected;
                if (disconnected != null)
                {
                    disconnected(this, EventArgs.Empty);
                }

                // Set the reconnect timer to try reconnection in a configured interval
                _reconnectTimer.Change(_managerReconnectIntervalMilliseconds, _managerReconnectIntervalMilliseconds);

                // Set connected to false
                _isConnected = false;
            }
        }

        /// <summary>
        /// Connects or reconnects to the server.
        /// </summary>
        /// <param name="state">The state. Ignored but required for timer callback methods. Pass null.</param>
        private void ReconnectToServer(object state)
        {
            // Check if already reconnected - ahead of the lock for speed
            if (_isConnected)
            {
                // Do nothing
                return;
            }

            // Lock to ensure atomic operations
            lock (_reconnectLock)
            {
                // Double check if already reconnected
                if (_isConnected)
                {
                    // Do nothing
                    return;
                }

                // Close and abort the Proxy Communication if needed
                try
                {
                    // Attempt the close
                    _proxyComm.Close();
                }
                catch
                {
                    // Close failed, abort it
                    _proxyComm.Abort();
                }

                // Re-initialize the Proxy
                _proxy = _channelFactory.CreateChannel();
                _proxyComm = _proxy as ICommunicationObject;

                // Attempt the actual reconnection
                try
                {
                    // Try to actually register
                    _proxy.Register(CacheHostInformation.HostAddress, MemCacheContainer.Instance.GetCount());
                }
                catch
                {
                    // Reconnection failed, so we're done (the Timer will retry)
                    return;
                }

                // Register for disconnection events
                _proxyComm.Closed += DisconnectFromServer;
                _proxyComm.Faulted += DisconnectFromServer;

                // If we got here, we're back online, adjust reconnected status
                _isConnected = true;

                // Disable the reconnect timer to stop trying to reconnect
                _reconnectTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // Fire the reconnected event
                var reconnected = Reconnected;
                if (reconnected != null)
                {
                    reconnected(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Creates a configured net tcp binding for communication.
        /// </summary>
        /// <returns>A configured net tcp binding.</returns>
        private NetTcpBinding CreateNetTcpBinding()
        {
            var netTcpBinding = new NetTcpBinding(SecurityMode.None, false)
            {
                CloseTimeout = TimeSpan.FromSeconds(15),
                OpenTimeout = TimeSpan.FromSeconds(15),
                SendTimeout = TimeSpan.FromSeconds(15),
                ReceiveTimeout = TimeSpan.FromSeconds(15),
                Namespace = "http://schemas.getdache.net/cachemanager",
                MaxBufferSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxConnections = 2000,
                ListenBacklog = 2000,
                ReliableSession = new OptionalReliableSession
                {
                    Enabled = false
                }
            };

            // Set reader quotas
            netTcpBinding.ReaderQuotas.MaxDepth = 64;
            netTcpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;

            return netTcpBinding;
        }
    }
}

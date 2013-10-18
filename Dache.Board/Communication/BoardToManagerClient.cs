using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Web;
using Dache.Communication.BoardToManager;

namespace Dache.Board.Communication
{
    /// <summary>
    /// Encapsulates a WCF Dacheboard to cache manager client.
    /// </summary>
    internal class BoardToManagerClient : IBoardToManagerContract
    {
        // The WCF channel factory
        private readonly ChannelFactory<IBoardToManagerContract> _channelFactory = null;
        // The WCF proxy
        private IBoardToManagerContract _proxy = null;
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
        /// <param name="managerAddress">The manager address.</param>
        /// <param name="managerReconnectIntervalMilliseconds">The cache manager reconnect interval, in milliseconds.</param>
        public BoardToManagerClient(string managerAddress, int managerReconnectIntervalMilliseconds)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(managerAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "managerAddress");
            }
            if (managerReconnectIntervalMilliseconds <= 0)
            {
                throw new ArgumentException("must be greater than 0", "managerReconnectIntervalMilliseconds");
            }

            // Build the endpoint address
            var endpointAddress = new EndpointAddress(managerAddress);
            // Build the net tcp binding
            var netTcpBinding = CreateNetTcpBinding();

            // Initialize the channel factory with the binding and endpoint address
            _channelFactory = new ChannelFactory<IBoardToManagerContract>(netTcpBinding, endpointAddress);

            // Set the cache manager reconnect interval
            _managerReconnectIntervalMilliseconds = managerReconnectIntervalMilliseconds;

            // Initialize WCF
            _proxy = _channelFactory.CreateChannel();
            _proxyComm = _proxy as ICommunicationObject;

            // Set connected before opening to avoid a race
            _isConnected = true;

            // Initialize and configure the reconnect timer to never fire
            _reconnectTimer = new Timer(ReconnectToServer, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Obtains performance information from the cache manager.
        /// </summary>
        /// <returns>The performance counters indexed at the key of cache host address.</returns>
        public IList<KeyValuePair<string, PerformanceCounter[]>> GetPerformanceInformation()
        {
            try
            {
                return _proxy.GetPerformanceInformation();
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Return null
                return null;
            }
        }

        /// <summary>
        /// Closes the connection to the cache host.
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
        /// Event that fires when the cache host is disconnected from a cache host.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Event that fires when the cache host is successfully reconnected to a disconnected cache host.
        /// </summary>
        public event EventHandler Reconnected;

        /// <summary>
        /// Outputs a human-friendly Dacheboard address and port.
        /// </summary>
        /// <returns>A string containing the Dacheboard address and port.</returns>
        public override string ToString()
        {
            return _channelFactory.Endpoint.Address.Uri.Host + ":" + _channelFactory.Endpoint.Address.Uri.Port;
        }

        /// <summary>
        /// Makes the client enter the disconnected state.
        /// </summary>
        private void DisconnectFromServer()
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
                    _proxyComm.Open();
                }
                catch
                {
                    // Reconnection failed, so we're done (the Timer will retry)
                    return;
                }

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
                ReceiveTimeout = TimeSpan.MaxValue,
                Namespace = "http://schemas.getdache.net/dacheboard",
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
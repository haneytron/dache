using Dache.Communication.ClientToCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;

namespace Dache.Client
{
    /// <summary>
    /// Encapsulates a WCF cache host client.
    /// </summary>
    internal class CommunicationClient : IClientToCacheContract
    {
        // The WCF channel factory
        private readonly ChannelFactory<IClientToCacheContract> _channelFactory = null;
        // The WCF proxy
        private IClientToCacheContract _proxy = null;
        // The WCF proxy communication object
        private ICommunicationObject _proxyComm = null;

        // The cache host reconnect interval, in milliseconds
        private readonly int _hostReconnectIntervalMilliseconds = 0;

        // Whether or not the communication client is connected
        private volatile bool _isConnected = false;
        // The lock object used for reconnection
        private readonly object _reconnectLock = new object();
        // The timer used to reconnect to the server
        private readonly Timer _reconnectTimer = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="endpointAddress">The endpoint address.</param>
        /// <param name="hostReconnectIntervalMilliseconds">The cache host reconnect interval, in milliseconds.</param>
        public CommunicationClient(Binding binding, EndpointAddress endpointAddress, int hostReconnectIntervalMilliseconds)
        {
            // Sanitize
            if (binding == null)
            {
                throw new ArgumentNullException("binding");
            }
            if (endpointAddress == null)
            {
                throw new ArgumentNullException("endpointAddress");
            }
            if (hostReconnectIntervalMilliseconds <= 0)
            {
                throw new ArgumentException("must be greater than 0", "hostReconnectIntervalMilliseconds");
            }

            // Initialize the channel factory with the binding and endpoint address
            _channelFactory = new ChannelFactory<IClientToCacheContract>(binding, endpointAddress);

            // Set the cache host reconnect interval
            _hostReconnectIntervalMilliseconds = hostReconnectIntervalMilliseconds;

            // Initialize WCF
            _proxy = _channelFactory.CreateChannel();
            _proxyComm = _proxy as ICommunicationObject;

            // Set connected before opening to avoid a race
            _isConnected = true;

            // Initialize and configure the reconnect timer to never fire
            _reconnectTimer = new Timer(ReconnectToServer, null, Timeout.Infinite, Timeout.Infinite); 
        }

        /// <summary>
        /// Gets the serialized object stored at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>The serialized object.</returns>
        public byte[] Get(string cacheKey)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            try
            {
                return _proxy.Get(cacheKey);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Gets the serialized objects stored at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <param name="isClientRequest">Whether or not the request is from a client.</param> 
        /// <returns>A list of the serialized objects.</returns>
        public IList<byte[]> GetMany(IEnumerable<string> cacheKeys, bool isClientRequest)
        {
            // Sanitize
            if (cacheKeys == null)
            {
                throw new ArgumentNullException("cacheKeys");
            }
            if (!cacheKeys.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeys");
            }

            try
            {
                return _proxy.GetMany(cacheKeys, isClientRequest);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Gets all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="isClientRequest">Whether or not the request is from a client.</param>
        /// <returns>A list of the serialized objects.</returns>
        public IList<byte[]> GetTagged(string tagName, bool isClientRequest)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                return _proxy.GetTagged(tagName, isClientRequest);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            try
            {
                _proxy.AddOrUpdate(cacheKey, serializedObject);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            try
            {
                _proxy.AddOrUpdate(cacheKey, serializedObject, absoluteExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }
        
        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            try
            {
                _proxy.AddOrUpdate(cacheKey, serializedObject, slidingExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdateMany(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }

            try
            {
                _proxy.AddOrUpdateMany(cacheKeysAndSerializedObjects);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateMany(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }

            try
            {
                _proxy.AddOrUpdateMany(cacheKeysAndSerializedObjects, absoluteExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateMany(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }

            try
            {
                _proxy.AddOrUpdateMany(cacheKeysAndSerializedObjects, slidingExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key and associates it with the given tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(string cacheKey, byte[] serializedObject, string tagName)
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

            try
            {
                _proxy.AddOrUpdateTagged(cacheKey, serializedObject, tagName);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key and associates it with the given tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateTagged(string cacheKey, byte[] serializedObject, string tagName, DateTimeOffset absoluteExpiration)
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

            try
            {
                _proxy.AddOrUpdateTagged(cacheKey, serializedObject, tagName, absoluteExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key and associates it with the given tag name.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(string cacheKey, byte[] serializedObject, string tagName, TimeSpan slidingExpiration)
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

            try
            {
                _proxy.AddOrUpdateTagged(cacheKey, serializedObject, tagName, slidingExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateManyTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                _proxy.AddOrUpdateManyTagged(cacheKeysAndSerializedObjects, tagName);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateManyTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, DateTimeOffset absoluteExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                _proxy.AddOrUpdateManyTagged(cacheKeysAndSerializedObjects, tagName, absoluteExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateManyTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (!cacheKeysAndSerializedObjects.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                _proxy.AddOrUpdateManyTagged(cacheKeysAndSerializedObjects, tagName, slidingExpiration);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Removes the serialized object at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public void Remove(string cacheKey)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "cacheKey");
            }

            try
            {
                _proxy.Remove(cacheKey);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Removes the serialized objects at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        public void RemoveMany(IEnumerable<string> cacheKeys)
        {
            // Sanitize
            if (cacheKeys == null)
            {
                throw new ArgumentNullException("cacheKeys");
            }
            if (!cacheKeys.Any())
            {
                throw new ArgumentException("must have at least one element", "cacheKeys");
            }

            try
            {
                _proxy.RemoveMany(cacheKeys);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        /// <param name="isClientRequest">Whether or not the request is from a client.</param>
        public void RemoveTagged(string tagName, bool isClientRequest)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                _proxy.RemoveTagged(tagName, isClientRequest);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        /// <summary>
        /// Outputs a human-friendly cache host address and port.
        /// </summary>
        /// <returns>A string containing the cache host address and port.</returns>
        public override string ToString()
        {
            return _channelFactory.Endpoint.Address.Uri.Host + ":" + _channelFactory.Endpoint.Address.Uri.Port;
        }

        /// <summary>
        /// Event that fires when the cache client is disconnected from a cache host.
        /// </summary>
        public event EventHandler Disconnected;

        /// <summary>
        /// Event that fires when the cache client is successfully reconnected to a disconnected cache host.
        /// </summary>
        public event EventHandler Reconnected;

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
                _reconnectTimer.Change(_hostReconnectIntervalMilliseconds, _hostReconnectIntervalMilliseconds);

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
    }
}

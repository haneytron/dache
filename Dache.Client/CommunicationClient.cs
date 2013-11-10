using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Dache.CacheHost.Communication;

namespace Dache.Client
{
    /// <summary>
    /// Encapsulates a WCF cache host client.
    /// </summary>
    internal class CommunicationClient : ICacheHostContract
    {
        // The synchronous client socket
        private Socket _client = null;
        // The client multiplexer
        private readonly Dictionary<int, KeyValuePair<DacheProtocolHelper.ClientStateObject, ManualResetEvent>> _clientMultiplexer = null;
        // The client multiplexer reader writer lock
        private readonly ReaderWriterLockSlim _clientMultiplexerLock = new ReaderWriterLockSlim();
        // The pool of manual reset events
        private readonly Pool<ManualResetEvent> _manualResetEventPool = null;
        // The pool of state objects
        private readonly Pool<DacheProtocolHelper.ClientStateObject> _stateObjectPool = null;

        // The remote endpoint
        private readonly IPEndPoint _remoteEndPoint = null;
        // The receive buffer
        private readonly byte[] _receiveBuffer = null;
        // The maximum number of simultaneous connections
        private readonly int _maximumConnections = 0;
        // The semaphore that enforces the maximum numbers of simultaneous connections
        private readonly Semaphore _maxConnectionsSemaphore;
        // The receive buffer size
        private readonly int _receiveBufferSize = 0;
        // The cache host reconnect interval, in milliseconds
        private readonly int _hostReconnectIntervalMilliseconds = 0;

        // Whether or not the communication client is connected
        private volatile bool _isConnected = true;
        // The lock object used for reconnection
        private readonly object _reconnectLock = new object();
        // The timer used to reconnect to the server
        private readonly Timer _reconnectTimer = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        /// <param name="maximumConnections">The maximum number of simultaneous connections.</param>
        /// <param name="receiveBufferSize">The buffer size to use for receiving data.</param>
        /// <param name="hostReconnectIntervalMilliseconds">The cache host reconnect interval, in milliseconds.</param>
        public CommunicationClient(string address, int port, int hostReconnectIntervalMilliseconds, int maximumConnections, int receiveBufferSize)
        {
            // Sanitize
            if (String.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "address");
            }
            if (port <= 0)
            {
                throw new ArgumentException("must be greater than 0", "port");
            }
            if (hostReconnectIntervalMilliseconds <= 0)
            {
                throw new ArgumentException("must be greater than 0", "hostReconnectIntervalMilliseconds");
            }
            if (maximumConnections <= 0)
            {
                throw new ArgumentException("must be greater than 0", "maximumConnections");
            }
            if (receiveBufferSize <= 512)
            {
                throw new ArgumentException("cannot be less than 512", "receiveBufferSize");
            }

            // Set maximum connections and receive buffer size
            _maximumConnections = maximumConnections;
            _maxConnectionsSemaphore = new Semaphore(maximumConnections, maximumConnections);
            _receiveBufferSize = receiveBufferSize;

            // Define the client multiplexer
            _clientMultiplexer = new Dictionary<int, KeyValuePair<DacheProtocolHelper.ClientStateObject, ManualResetEvent>>(_maximumConnections);

            // Initialize and populate the pools
            _manualResetEventPool = new Pool<ManualResetEvent>(_maximumConnections, () => new ManualResetEvent(false));
            _stateObjectPool = new Pool<DacheProtocolHelper.ClientStateObject>(_maximumConnections, () => new DacheProtocolHelper.ClientStateObject(_receiveBufferSize));
            for (int i = 0; i < _maximumConnections; i++)
            {
                _manualResetEventPool.Push(new ManualResetEvent(false));
                _stateObjectPool.Push(new DacheProtocolHelper.ClientStateObject(_receiveBufferSize));
            }

            // Initialize buffer
            _receiveBuffer = new byte[_receiveBufferSize];

            // Establish the remote endpoint for the socket
            var ipHostInfo = Dns.GetHostEntry(address);
            var ipAddress = ipHostInfo.AddressList.First(i => i.AddressFamily == AddressFamily.InterNetwork);
            _remoteEndPoint = new IPEndPoint(ipAddress, port);

            // Set the cache host reconnect interval
            _hostReconnectIntervalMilliseconds = hostReconnectIntervalMilliseconds;

            // Initialize reconnect timer
            _reconnectTimer = new Timer(ReconnectToServer, null, Timeout.Infinite, Timeout.Infinite); 
        }

        public void Connect()
        {
            // Define the socket
            _client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // Disable the Nagle algorithm
            _client.NoDelay = true;

            try
            {
                _client.Connect(_remoteEndPoint);

                // Perpetually receive
                _client.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, 0, ReceiveCallback, null);
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
        /// Gets the serialized object stored at the given cache key from the cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <returns>The serialized object.</returns>
        public byte[] Get(string cacheKey)
        {
            var result = Get(new[] { cacheKey });
            if (result == null || result.Count == 0)
            {
                return null;
            }
            return result[0];
        }

        /// <summary>
        /// Gets the serialized objects stored at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> Get(IEnumerable<string> cacheKeys)
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

            int cacheKeysCount = 0;

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("get");
                    foreach (var cacheKey in cacheKeys)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKey);
                        cacheKeysCount++;
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeys);

                // Send
                Send(command, true);
                // Receive
                var commandResult = Receive();

                // Verify that we got something
                if (commandResult == null)
                {
                    return null;
                }
                
                // Parse command from bytes
                var commandResultParts = commandResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return ParseCacheObjects(commandResultParts);
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
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> GetTagged(string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "tagName");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("get-tag {0}", tagName);
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.Literal);

                // Send
                Send(command, true);
                // Receive
                var commandResult = Receive();

                // Verify that we got something
                if (commandResult == null)
                {
                    return null;
                }

                // Parse command from bytes
                var commandResultParts = commandResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return ParseCacheObjects(commandResultParts);
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
            AddOrUpdate(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) });
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, DateTimeOffset absoluteExpiration)
        {
            AddOrUpdate(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, absoluteExpiration);
        }
        
        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, TimeSpan slidingExpiration)
        {
            AddOrUpdate(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, slidingExpiration);
        }

        /// <summary>
        /// Adds or updates an interned serialized object in the cache at the given cache key.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        public void AddOrUpdateInterned(string cacheKey, byte[] serializedObject)
        {
            AddOrUpdateInterned(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) });
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set");
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, DateTimeOffset absoluteExpiration)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set {0}", absoluteExpiration.ToString(DacheProtocolHelper.AbsoluteExpirationFormat));
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, TimeSpan slidingExpiration)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set {0}", (int)slidingExpiration.TotalSeconds);
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        public void AddOrUpdateInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set-intern");
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
            AddOrUpdateTagged(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName);
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
            AddOrUpdateTagged(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName, absoluteExpiration);
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
            AddOrUpdateTagged(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName, slidingExpiration);
        }

        /// <summary>
        /// Adds or updates the interned serialized object in the cache at the given cache key and associates it with the given tag name.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTaggedInterned(string cacheKey, byte[] serializedObject, string tagName)
        {
            AddOrUpdateTaggedInterned(new[] { new KeyValuePair<string, byte[]>(cacheKey, serializedObject) }, tagName);
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set-tag {0}", tagName);
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, DateTimeOffset absoluteExpiration)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set-tag {0} {1}", tagName, absoluteExpiration.ToString(DacheProtocolHelper.AbsoluteExpirationFormat));
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set-tag {0} {1}", tagName, (int)slidingExpiration.TotalSeconds);
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
        /// Adds or updates the interned serialized objects in the cache at the given cache keys.
        /// NOTE: interned objects use significantly less memory when placed in the cache multiple times however cannot expire or be evicted. 
        /// You must remove them manually when appropriate or else you may face a memory leak.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        public void AddOrUpdateTaggedInterned(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("set-tag-intern {0}", tagName);
                    foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                        memoryStream.WriteSpace();
                        memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                    }
                    command = memoryStream.ToArray();
                }
                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);

                // Send
                Send(command, false);
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
            Remove(new[] { cacheKey });
        }

        /// <summary>
        /// Removes the serialized objects at the given cache keys from the cache.
        /// </summary>
        /// <param name="cacheKeys">The cache keys.</param>
        public void Remove(IEnumerable<string> cacheKeys)
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
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("del");
                    foreach (var cacheKey in cacheKeys)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKey);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeys);

                // Send
                Send(command, false);
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
        public void RemoveTagged(string tagName)
        {
            RemoveTagged(new[] { tagName });
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag names.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        public void RemoveTagged(IEnumerable<string> tagNames)
        {
            // Sanitize
            if (tagNames == null)
            {
                throw new ArgumentNullException("tagNames");
            }

            try
            {
                byte[] command = null;
                using (var memoryStream = new MemoryStream())
                {
                    // Write default control bytes - will be replaced later
                    memoryStream.WriteControlBytesDefault();
                    memoryStream.Write("del-tag");
                    foreach (var tagName in tagNames)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(tagName);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                command.SetControlBytes(GetCurrentThreadId(), DacheProtocolHelper.MessageType.RepeatingCacheKeys);

                // Send
                Send(command, false);
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
            return _remoteEndPoint.Address + ":" + _remoteEndPoint.Port;
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

                // Ensure socket is properly closed
                try
                {
                    _client.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                    // Ignore
                }

                // Close the client
                _client.Close();

                try
                {
                    // Reconnect
                    Connect();
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

        private void Send(byte[] command, bool registerForResponse)
        {
            // Check if we need to register with the multiplexer
            if (registerForResponse)
            {
                var threadId = GetCurrentThreadId();
                EnrollMultiplexer(threadId);
            }

            for (int i = 0; i < command.Length; i = i + _receiveBufferSize)
            {
                _client.BeginSend(command, i, Math.Min(_receiveBufferSize, command.Length - i), 0, SendCallback, null);
            }
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            // End the send
            _client.EndSend(asyncResult);
        }

        private string Receive()
        {
            // Get this thread's state object and manual reset event
            var threadId = GetCurrentThreadId();
            var stateAndManualResetEvent = GetMultiplexerValue(threadId);

            // Wait for our message to go ahead from the receive callback
            stateAndManualResetEvent.Value.WaitOne();

            // Now get the command string
            var result = DacheProtocolHelper.CommunicationEncoding.GetString(stateAndManualResetEvent.Key.Data);

            // Finally remove the thread from the multiplexer
            UnenrollMultiplexer(threadId);

            return result;
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            // If this is the first receive call for message frame, the state will be null
            var state = asyncResult as DacheProtocolHelper.ClientStateObject;

            int bytesRead = _client.EndReceive(asyncResult);

            ProcessMessage(_receiveBuffer, state, bytesRead);
        }

        private KeyValuePair<DacheProtocolHelper.ClientStateObject, ManualResetEvent> GetMultiplexerValue(int threadId)
        {
            KeyValuePair<DacheProtocolHelper.ClientStateObject, ManualResetEvent> stateAndManualResetEvent;
            _clientMultiplexerLock.EnterReadLock();
            try
            {
                // Get from multiplexer by thread ID
                if (!_clientMultiplexer.TryGetValue(threadId, out stateAndManualResetEvent))
                {
                    throw new Exception("FATAL: multiplexer was missing entry for Thread ID " + threadId);
                }

                return stateAndManualResetEvent;
            }
            finally
            {
                _clientMultiplexerLock.ExitReadLock();
            }
        }

        private void EnrollMultiplexer(int threadId)
        {
            _clientMultiplexerLock.EnterWriteLock();
            try
            {
                // Add manual reset event for current thread
                _clientMultiplexer.Add(threadId, new KeyValuePair<DacheProtocolHelper.ClientStateObject, ManualResetEvent>(_stateObjectPool.Pop(), _manualResetEventPool.Pop()));
            }
            catch
            {
                throw new Exception("FATAL: multiplexer tried to add duplicate entry for Thread ID " + threadId);
            }
            finally
            {
                _clientMultiplexerLock.ExitWriteLock();
            }
        }

        private void UnenrollMultiplexer(int threadId)
        {
            KeyValuePair<DacheProtocolHelper.ClientStateObject, ManualResetEvent> stateAndManualResetEvent;
            _clientMultiplexerLock.EnterUpgradeableReadLock();
            try
            {
                // Get from multiplexer by thread ID
                if (!_clientMultiplexer.TryGetValue(threadId, out stateAndManualResetEvent))
                {
                    throw new Exception("FATAL: multiplexer was missing entry for Thread ID " + threadId);
                }

                _clientMultiplexerLock.EnterWriteLock();
                try
                {
                    // Remove entry
                    _clientMultiplexer.Remove(threadId);
                }
                finally
                {
                    _clientMultiplexerLock.ExitWriteLock();
                }
            }
            finally
            {
                _clientMultiplexerLock.ExitUpgradeableReadLock();
            }

            // Now return objects to pools
            _stateObjectPool.Push(stateAndManualResetEvent.Key);
            _manualResetEventPool.Push(stateAndManualResetEvent.Value);
        }

        private void SignalMultiplexer(int threadId)
        {
            KeyValuePair<DacheProtocolHelper.ClientStateObject, ManualResetEvent> stateAndManualResetEvent;
            _clientMultiplexerLock.EnterReadLock();
            try
            {
                // Get from multiplexer by thread ID
                if (!_clientMultiplexer.TryGetValue(threadId, out stateAndManualResetEvent))
                {
                    throw new Exception("FATAL: multiplexer was missing entry for Thread ID " + threadId);
                }

                stateAndManualResetEvent.Value.Set();
            }
            finally
            {
                _clientMultiplexerLock.ExitReadLock();
            }
        }

        private void ProcessMessage(byte[] buffer, DacheProtocolHelper.ClientStateObject state, int bytesRead)
        {
            int totalBytesToRead = state == null ? -1 : state.TotalBytesToRead;
            DacheProtocolHelper.MessageType messageType = state == null ? DacheProtocolHelper.MessageType.Literal : state.MessageType;

            // Read data
            if (totalBytesToRead != 0 && bytesRead > 0)
            {
                byte[] currentBuffer = null;

                // Check if we need to get our control byte values
                if (totalBytesToRead == -1)
                {
                    // Parse out control bytes
                    int threadId = 0;
                    var strippedBuffer = _receiveBuffer.RemoveControlByteValues(out totalBytesToRead, out threadId, out messageType);

                    // Check if we need to get this new state from the multiplexer
                    if (state == null)
                    {
                        // We do, so get state
                        state = GetMultiplexerValue(threadId).Key;
                    }

                    // Set values to state
                    state.TotalBytesToRead = totalBytesToRead;
                    state.ThreadId = threadId;
                    state.MessageType = messageType;

                    // Take control bytes off of bytes read
                    bytesRead -= DacheProtocolHelper.ControlBytesDefault.Length;

                    currentBuffer = strippedBuffer;
                }
                else
                {
                    currentBuffer = _receiveBuffer;
                }

                int numberOfBytesToRead = bytesRead > state.TotalBytesToRead ? state.TotalBytesToRead : bytesRead;
                state.Data = DacheProtocolHelper.Combine(state.Data, state.Data.Length, currentBuffer, numberOfBytesToRead);

                // Set total bytes read
                var originalTotalBytesToRead = state.TotalBytesToRead;
                state.TotalBytesToRead -= bytesRead;

                // Check if we have part of another message in our received bytes
                if (state.TotalBytesToRead < 0)
                {
                    // We do, so parse it out
                    var nextMessageFrameBytes = new byte[bytesRead - originalTotalBytesToRead];
                    Buffer.BlockCopy(_receiveBuffer, originalTotalBytesToRead, nextMessageFrameBytes, 0, nextMessageFrameBytes.Length);
                    // Set total bytes to read to 0
                    state.TotalBytesToRead = 0;
                    // Now we have the next message, so recursively process it
                    ProcessMessage(nextMessageFrameBytes, null, nextMessageFrameBytes.Length);
                }

                // Check if we're done with this message frame
                if (state.TotalBytesToRead > 0)
                {
                    // Read more
                    _client.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, 0, ReceiveCallback, state);
                    return;
                }

                // All done, so signal the event for this thread
                SignalMultiplexer(state.ThreadId);
            }
        }

        private static List<byte[]> ParseCacheObjects(string[] commandParts)
        {
            // Regular set
            var cacheObjects = new List<byte[]>(commandParts.Length);
            for (int i = 0; i < commandParts.Length; i ++)
            {
                cacheObjects.Add(Convert.FromBase64String(commandParts[i]));
            }
            return cacheObjects;
        }

        private static int GetCurrentThreadId()
        {
            return Thread.CurrentThread.ManagedThreadId;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using Dache.CacheHost.Communication;
using Dache.Client.Extensions;

namespace Dache.Client
{
    /// <summary>
    /// Encapsulates a WCF cache host client.
    /// </summary>
    internal class CommunicationClient : ICacheHostContract
    {
        // The client socket
        private Socket _client = null;

        // The remote endpoint
        private readonly IPEndPoint _remoteEndPoint = null;
        // The send buffer
        private readonly byte[] _sendBuffer = null;
        // The receive buffer
        private readonly byte[] _receiveBuffer = null;
        // The receive buffer size
        private readonly int _receiveBufferSize = 0;
        // The cache host reconnect interval, in milliseconds
        private readonly int _hostReconnectIntervalMilliseconds = 0;
        // The communication encoding
        public static readonly Encoding CommunicationEncoding = Encoding.UTF8;
        // The communication protocol control bytes default - 4 little endian bytes + 1 control byte
        private static readonly byte[] _controlBytesDefault = new byte[] { 0, 0, 0, 0, 0 };
        // The byte that represents a space
        private static readonly byte _spaceByte = CommunicationEncoding.GetBytes(" ")[0];
        // The absolute expiration format
        private const string _absoluteExpirationFormat = "yyMMddhhmmss";
        // The client lock
        private readonly ManualResetEvent _clientLock = new ManualResetEvent(true);

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
        /// <param name="receiveBufferSize">The buffer size to use for receiving data.</param>
        /// <param name="hostReconnectIntervalMilliseconds">The cache host reconnect interval, in milliseconds.</param>
        public CommunicationClient(string address, int port, int hostReconnectIntervalMilliseconds, int receiveBufferSize)
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
            if (receiveBufferSize <= 512)
            {
                throw new ArgumentException("cannot be less than 512", "receiveBufferSize");
            }

            // Set receive buffer size
            _receiveBufferSize = receiveBufferSize;

            // Initialize buffers
            _sendBuffer = new byte[_receiveBufferSize];
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
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        private void Receive()
        {
            _data = new byte[0];
            _client.BeginReceive( _receiveBuffer, 0, _receiveBuffer.Length, 0, ReceiveCallback, null);
        }

        private byte[] _data = null;
        private string _commandResult = null;

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            int bytesRead = 0;
            int totalBytesToRead = -1;
            MessageType messageType = MessageType.Literal;

            bytesRead = _client.EndReceive(asyncResult);

            // Read data
            if (totalBytesToRead != 0 && bytesRead > 0)
            {
                // Check if we need to decode little endian
                if (totalBytesToRead == -1)
                {
                    // Parse out control bytes
                    var strippedBuffer = RemoveControlByteValues(_receiveBuffer, out totalBytesToRead, out messageType);
                    // Take control bytes off of bytes read
                    bytesRead -= _controlBytesDefault.Length;

                    // Set data
                    _data = Combine(_data, _data.Length, strippedBuffer, bytesRead);
                }
                else
                {
                    _data = Combine(_data, _data.Length, _receiveBuffer, bytesRead);
                }

                // Set total bytes read
                totalBytesToRead -= bytesRead;

                if (totalBytesToRead > 0)
                {
                    // Read more
                    _client.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, 0, ReceiveCallback, null);
                }
            }

            // Parse command from bytes
            _commandResult = CommunicationEncoding.GetString(_data);
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
                    memoryStream.Write(_controlBytesDefault);
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
                SetControlBytes(command, MessageType.RepeatingCacheKeys);

                _clientLock.WaitOne();
                // Send
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
                _clientLock.WaitOne();
                // Receive
                Receive();
                var commandResult = _commandResult;
                _clientLock.Set();
                
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
                    memoryStream.Write(_controlBytesDefault);
                    memoryStream.Write("get-tag {0}", tagName);
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                SetControlBytes(command, MessageType.Literal);

                _clientLock.WaitOne();
                // Send
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
                _clientLock.WaitOne();
                // Receive
                Receive();
                var commandResult = _commandResult;
                _clientLock.Set();

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
                    memoryStream.Write(_controlBytesDefault);
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
            }
            catch
            {
                // Enter the disconnected state
                DisconnectFromServer();
                // Rethrow
                throw;
            }
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            // End the send
            _client.EndSend(asyncResult);
            // All done
            _clientLock.Set();
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
                    memoryStream.Write(_controlBytesDefault);
                    memoryStream.Write("set {0}", absoluteExpiration.ToString(_absoluteExpirationFormat));
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
                    memoryStream.Write("set-tag {0} {1}", tagName, absoluteExpiration.ToString(_absoluteExpirationFormat));
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
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
                SetControlBytes(command, MessageType.RepeatingCacheKeysAndObjects);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
                    memoryStream.Write("del");
                    foreach (var cacheKey in cacheKeys)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(cacheKey);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                SetControlBytes(command, MessageType.RepeatingCacheKeys);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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
                    memoryStream.Write(_controlBytesDefault);
                    memoryStream.Write("del-tag");
                    foreach (var tagName in tagNames)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(tagName);
                    }
                    command = memoryStream.ToArray();
                }

                // Set control bytes
                SetControlBytes(command, MessageType.RepeatingCacheKeys);

                // Send
                _clientLock.WaitOne();
                _client.BeginSend(command, 0, command.Length, 0, SendCallback, null);
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

        private byte[] RemoveControlByteValues(byte[] command, out int messageLength, out MessageType delimiterType)
        {
            messageLength = (command[3] << 24) | (command[2] << 16) | (command[1] << 8) | command[0];
            delimiterType = (MessageType)command[4];
            var result = new byte[command.Length - _controlBytesDefault.Length];
            Buffer.BlockCopy(command, 5, result, 0, result.Length);
            return result;
        }

        private void SetControlBytes(byte[] command, MessageType delimiterType)
        {
            var length = command.Length - _controlBytesDefault.Length;
            command[0] = (byte)length;
            command[1] = (byte)((length >> 8) & 0xFF);
            command[2] = (byte)((length >> 16) & 0xFF);
            command[3] = (byte)((length >> 24) & 0xFF);
            command[4] = Convert.ToByte((int)delimiterType);
        }

        private static byte[] Combine(byte[] first, byte[] second)
        {
            return Combine(first, first.Length, second, second.Length);
        }

        private static byte[] Combine(byte[] first, int firstLength, byte[] second, int secondLength)
        {
            byte[] ret = new byte[firstLength + secondLength];
            Buffer.BlockCopy(first, 0, ret, 0, firstLength);
            Buffer.BlockCopy(second, 0, ret, firstLength, secondLength);
            return ret;
        }

        private class StateObject
        {
            public Socket WorkSocket = null;
            public const int BufferSize = 8192;
            public byte[] Buffer = new byte[BufferSize];
            public byte[] Data = new byte[0];
            public MessageType MessageType = MessageType.Literal;
            public int TotalBytesToRead = -1;
        }

        private enum MessageType
        {
            /// <summary>
            /// No repeated items.
            /// </summary>
            Literal = 0,

            /// <summary>
            /// Repeating cache keys.
            /// </summary>
            RepeatingCacheKeys,

            /// <summary>
            /// Repeating cache objects.
            /// </summary>
            RepeatingCacheObjects,

            /// <summary>
            /// Repeating cache keys and objects in pairs.
            /// </summary>
            RepeatingCacheKeysAndObjects
        }
    }
}

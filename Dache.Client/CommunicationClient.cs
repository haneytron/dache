﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Dache.Core.Communication;
using SimplSockets;

namespace Dache.Client
{
    /// <summary>
    /// Encapsulates a cache host client.
    /// </summary>
    internal class CommunicationClient : ICacheHostContract
    {
        // The client socket
        private ISimplSocketClient _client = null;

        // The remote endpoint
        private readonly IPEndPoint _remoteEndPoint = null;
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
        /// <param name="messageBufferSize">The buffer size to use for sending and receiving data.</param>
        /// <param name="hostReconnectIntervalMilliseconds">The cache host reconnect interval, in milliseconds.</param>
        public CommunicationClient(string address, int port, int hostReconnectIntervalMilliseconds, int maximumConnections, int messageBufferSize)
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
            if (messageBufferSize <= 256)
            {
                throw new ArgumentException("cannot be less than 512", "messageBufferSize");
            }

            // Define the client
            _client = SimplSocket.CreateClient(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp),
                (sender, e) => { DisconnectFromServer(); }, messageBufferSize, maximumConnections, false);

            // Establish the remote endpoint for the socket
            var ipHostInfo = Dns.GetHostEntry(address);
            var ipAddress = ipHostInfo.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork) ?? IPAddress.Parse(address);
            _remoteEndPoint = new IPEndPoint(ipAddress, port);

            // Set the cache host reconnect interval
            _hostReconnectIntervalMilliseconds = hostReconnectIntervalMilliseconds;

            // Initialize reconnect timer
            _reconnectTimer = new Timer(ReconnectToServer, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Connects to the cache host.
        /// </summary>
        /// <returns>true if successful, false otherwise.</returns>
        public bool Connect()
        {
            var result = _client.Connect(_remoteEndPoint);
            if (!result)
            {
                // Disconnect the client
                DisconnectFromServer();
            }
            return result;
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
                memoryStream.Write("get");
                foreach (var cacheKey in cacheKeys)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(cacheKey);
                }
                command = memoryStream.ToArray();
            }

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeys);
            // Send and receive
            command = _client.SendReceive(command);
            // Parse string
            var commandResult = DacheProtocolHelper.CommunicationEncoding.GetString(command);

            // Verify that we got something
            if (commandResult == null)
            {
                return null;
            }

            // Parse command from bytes
            var commandResultParts = commandResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return ParseCacheObjects(commandResultParts);
        }

        /// <summary>
        /// Gets all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> GetTagged(IEnumerable<string> tagNames)
        {
            // Sanitize
            if (tagNames == null)
            {
                throw new ArgumentNullException("tagNames");
            }
            if (!tagNames.Any())
            {
                throw new ArgumentException("must have at least one element", "tagNames");
            }

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
                memoryStream.Write("get-tag");
                foreach (var tagName in tagNames)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(tagName);
                }
                command = memoryStream.ToArray();
            }

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeys);
            // Send and receive
            command = _client.SendReceive(command);
            // Parse string
            var commandResult = DacheProtocolHelper.CommunicationEncoding.GetString(command);

            // Verify that we got something
            if (commandResult == null)
            {
                return null;
            }

            // Parse command from bytes
            var commandResultParts = commandResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return ParseCacheObjects(commandResultParts);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
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

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects);
            // Send
            _client.Send(command);
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

            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
                memoryStream.Write("del");
                foreach (var cacheKey in cacheKeys)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(cacheKey);
                }
                command = memoryStream.ToArray();
            }

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeys);
            // Send
            _client.Send(command);
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag names and optionally with keys matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        public void RemoveTagged(IEnumerable<string> tagNames, string pattern = "*")
        {
            // Sanitize
            if (tagNames == null)
            {
                throw new ArgumentNullException("tagNames");
            }
            if (!tagNames.Any())
            {
                throw new ArgumentException("must have at least one element", "tagNames");
            }
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }

            byte[] command;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
                memoryStream.Write("del-tag");
                memoryStream.WriteSpace();
                memoryStream.Write(pattern);
                foreach (var tagName in tagNames)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(tagName);
                }
                command = memoryStream.ToArray();
            }

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.RepeatingCacheKeys);
            // Send
            _client.Send(command);
        }

        /// <summary>
        /// Gets all cache keys, optionally matching the provided pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern</returns>
        public List<byte[]> GetCacheKeys(string pattern = "*")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }

            byte[] command;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
                memoryStream.Write("keys {0}", pattern);
                command = memoryStream.ToArray();
            }

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.Literal);
            // Send and receive
            var rawResult = _client.SendReceive(command);

            // Verify that we got something
            if (rawResult == null)
            {
                return null;
            }

            // Parse string
            var decodedResult = DacheProtocolHelper.CommunicationEncoding.GetString(rawResult);
            if (string.IsNullOrWhiteSpace(decodedResult))
            {
                return null;
            }

            // Parse command from bytes
            var commandResultParts = decodedResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return ParseCacheObjects(commandResultParts);
        }

        /// <summary>
        /// Gets all cache keys associated with the given tag names and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<byte[]> GetCacheKeysTagged(IEnumerable<string> tagNames, string pattern = "*")
        {
            if (tagNames == null)
            {
                throw new ArgumentNullException("tagNames");
            }
            if (!tagNames.Any())
            {
                throw new ArgumentException("must have at least one element", "tagNames");
            }
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }

            byte[] command;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
                memoryStream.Write("keys-tag");
                memoryStream.WriteSpace();
                memoryStream.Write(pattern);
                foreach (var tagName in tagNames)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(tagName);
                }
                command = memoryStream.ToArray();
            }

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.Literal);
            // Send and receive
            var rawResult = _client.SendReceive(command);

            // Verify that we got something
            if (rawResult == null)
            {
                return null;
            }

            // Parse string
            var decodedResult = DacheProtocolHelper.CommunicationEncoding.GetString(rawResult);
            if (string.IsNullOrWhiteSpace(decodedResult))
            {
                return null;
            }

            // Parse command from bytes
            var commandResultParts = decodedResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return ParseCacheObjects(commandResultParts);
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear()
        {
            byte[] command;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.WriteControlBytePlaceHolder();
                memoryStream.Write("clear");
                command = memoryStream.ToArray();
            }

            // Set control byte
            command.SetControlByte(DacheProtocolHelper.MessageType.Literal);
            // Send
            _client.Send(command);
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
                    _client.Close();
                }
                catch
                {
                    // Ignore
                }

                // Close the client
                _client.Close();

                // Reconnect
                if (!Connect())
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
        /// Parses the command parts onto byte arrays.
        /// </summary>
        /// <param name="commandParts">The command parts.</param>
        /// <returns>A list of byte arrays.</returns>
        private static List<byte[]> ParseCacheObjects(string[] commandParts)
        {
            // Regular set
            var cacheObjects = new List<byte[]>(commandParts.Length);
            for (int i = 0; i < commandParts.Length; i++)
            {
                cacheObjects.Add(Convert.FromBase64String(commandParts[i]));
            }
            return cacheObjects;
        }
    }
}

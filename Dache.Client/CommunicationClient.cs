using System;
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
        private volatile bool _isConnected = false;
        // The lock object used for reconnection
        private readonly object _reconnectLock = new object();
        // The timer used to reconnect to the server
        private readonly Timer _reconnectTimer = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        /// <param name="messageBufferSize">The buffer size to use for sending and receiving data.</param>
        /// <param name="timeoutMilliseconds">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize">The maximum message size, in bytes.</param>
        /// <param name="hostReconnectIntervalMilliseconds">The cache host reconnect interval, in milliseconds.</param>
        public CommunicationClient(string address, int port, int hostReconnectIntervalMilliseconds, int messageBufferSize, int timeoutMilliseconds, int maxMessageSize)
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

            // Define the client
            _client = new SimplSocketClient(() => new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp), messageBufferSize: messageBufferSize, 
                communicationTimeout: timeoutMilliseconds, maxMessageSize: maxMessageSize);

            // Wire into events
            _client.MessageReceived += (sender, e) =>
            {
                var messageReceived = MessageReceived;
                if (messageReceived != null)
                {
                    messageReceived(sender, e);
                }
            };
            _client.Error += (sender, e) => { DisconnectFromServer(); };

            // Establish the remote endpoint for the socket
            IPAddress ipAddress = null;
            if (!IPAddress.TryParse(address, out ipAddress))
            {
                // Try and get DNS value
                var ipHostInfo = Dns.GetHostEntry(address);
                ipAddress = ipHostInfo.AddressList.FirstOrDefault(i =>
                    i.AddressFamily == AddressFamily.InterNetwork
                    // ignore link-local addresses (the 169.254.* is documented in IETF RFC 3927 => http://www.ietf.org/rfc/rfc3927.txt)
                    && !i.ToString().StartsWith("169.254."));
                
                if (ipAddress == null)
                {
                    throw new ArgumentException("must be a valid host name or IP address", "address");
                }
            }

            _remoteEndPoint = new IPEndPoint(ipAddress, port);

            // Set the cache host reconnect interval
            _hostReconnectIntervalMilliseconds = hostReconnectIntervalMilliseconds;

            // Initialize reconnect timer
            _reconnectTimer = new Timer(ReconnectToServer, null, Timeout.Infinite, Timeout.Infinite);

            // Always assume initially connected
            _isConnected = true;
        }

        /// <summary>
        /// Connects to the cache host.
        /// </summary>
        /// <returns>true if successful, false otherwise.</returns>
        public bool Connect()
        {
            return _client.Connect(_remoteEndPoint);
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

            return SendGet(cacheKeys);
        }

        /// <summary>
        /// Gets all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>A list of the serialized objects.</returns>
        public List<byte[]> GetTagged(IEnumerable<string> tagNames, string pattern = "*")
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

            return SendGet(tagNames, isTagNames: true, pattern: pattern);
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="absoluteExpiration">The absolute expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="slidingExpiration">The sliding expiration. NOTE: if both absolute and sliding expiration are set, sliding expiration will be ignored.</param>
        /// <param name="notifyRemoved">Whether or not to notify the client when the cached item is removed from the cache.</param>
        /// <param name="isInterned">Whether or not to intern the objects. NOTE: interned objects use significantly less memory when 
        /// placed in the cache multiple times however cannot expire or be evicted. You must remove them manually when appropriate 
        /// or else you will face a memory leak. If specified, absoluteExpiration, slidingExpiration, and notifyRemoved are ignored.</param>
        public void AddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName = null, DateTimeOffset? absoluteExpiration = null, TimeSpan? slidingExpiration = null, bool notifyRemoved = false, bool isInterned = false)
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

            SendAddOrUpdate(cacheKeysAndSerializedObjects, tagName: tagName, absoluteExpiration: absoluteExpiration, slidingExpiration: slidingExpiration, notifyRemoved: notifyRemoved, isInterned: isInterned);
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

            SendRemove(cacheKeys);
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag names and optionally with keys matching the given pattern.
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

            SendRemove(tagNames, isTagNames: true, pattern: pattern);
        }

        /// <summary>
        /// Gets all cache keys, optionally matching the provided pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<string> GetCacheKeys(string pattern = "*")
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "pattern");
            }

            return SendGetCacheKeys(pattern: pattern);
        }

        /// <summary>
        /// Gets all cache keys associated with the given tag names and optionally matching the given pattern.
        /// WARNING: THIS IS A VERY EXPENSIVE OPERATION FOR LARGE TAG CACHES. USE WITH CAUTION.
        /// </summary>
        /// <param name="tagNames">The tag names.</param>
        /// <param name="pattern">The search pattern (RegEx). Optional. If not specified, the default of "*" is used to indicate match all.</param>
        /// <returns>The list of cache keys matching the provided pattern.</returns>
        public List<string> GetCacheKeysTagged(IEnumerable<string> tagNames, string pattern = "*")
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

            return SendGetCacheKeys(tagNames: tagNames, pattern: pattern);
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Clear()
        {
            byte[] command;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Write("clear");
                command = memoryStream.ToArray();
            }

            // Send
            _client.Send(command);
        }

        private List<byte[]> SendGet(IEnumerable<string> cacheKeysOrTagNames, bool isTagNames = false, string pattern = null)
        {
            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Write("get");

                if (isTagNames)
                {
                    if (pattern != null)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(pattern);
                        memoryStream.WriteSpace();
                    }

                    memoryStream.WriteSpace();
                    memoryStream.Write("-t");
                }

                foreach (var cacheKeysOrTagName in cacheKeysOrTagNames)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(cacheKeysOrTagName);
                }
                command = memoryStream.ToArray();
            }

            // Send and receive
            command = _client.SendReceive(command);
            // Parse string
            var commandResult = DacheProtocolHelper.CommunicationEncoding.GetString(command);

            // Verify that we got something
            if (commandResult == null || commandResult == "\0")
            {
                return null;
            }

            // Parse command from bytes
            var commandResultParts = commandResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return ParseCacheObjects(commandResultParts);
        }

        private void SendAddOrUpdate(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName = null,
            DateTimeOffset? absoluteExpiration = null, TimeSpan? slidingExpiration = null, bool notifyRemoved = false, bool isInterned = false)
        {
            byte[] command = null;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Write("set");

                if (isInterned)
                {
                    // If interned, expirations and callback notifications are ignored
                    memoryStream.Write(" -i");
                }
                else
                {
                    // Absolute expiration
                    if (absoluteExpiration.HasValue)
                    {
                        memoryStream.Write(" -a {0}", absoluteExpiration.Value.UtcDateTime.ToString(DacheProtocolHelper.AbsoluteExpirationFormat));
                    }
                    // Sliding expiration
                    else if (slidingExpiration.HasValue)
                    {
                        memoryStream.Write(" -s {0}", (int)slidingExpiration.Value.TotalSeconds);
                    }

                    // Notify removed
                    if (notifyRemoved)
                    {
                        memoryStream.Write(" -c");
                    }
                }

                // Tag name
                if (!string.IsNullOrWhiteSpace(tagName))
                {
                    memoryStream.Write(" -t {0}", tagName);
                }

                foreach (var cacheKeyAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(cacheKeyAndSerializedObjectKvp.Key);
                    memoryStream.WriteSpace();
                    memoryStream.WriteBase64(cacheKeyAndSerializedObjectKvp.Value);
                }
                command = memoryStream.ToArray();
            }

            // Send
            _client.Send(command);
        }

        private void SendRemove(IEnumerable<string> cacheKeysOrTagNames, bool isTagNames = false, string pattern = "*")
        {
            byte[] command;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Write("del ");

                if (isTagNames)
                {
                    memoryStream.Write("{0} -t ", pattern);
                }

                foreach (var cacheKeysOrTagName in cacheKeysOrTagNames)
                {
                    memoryStream.WriteSpace();
                    memoryStream.Write(cacheKeysOrTagName);
                }
                command = memoryStream.ToArray();
            }

            // Send
            _client.Send(command);
        }

        private List<string> SendGetCacheKeys(IEnumerable<string> tagNames = null, string pattern = "*")
        {
            byte[] command;
            using (var memoryStream = new MemoryStream())
            {
                memoryStream.Write("keys {0}", pattern);

                if (tagNames != null)
                {
                    memoryStream.Write(" -t");

                    foreach (var tagName in tagNames)
                    {
                        memoryStream.WriteSpace();
                        memoryStream.Write(tagName);
                    }
                }

                command = memoryStream.ToArray();
            }

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

            return new List<string>(decodedResult.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
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
        /// An event that is fired whenever a message is received from the cache host.
        /// </summary>
        public event EventHandler<MessageReceivedArgs> MessageReceived;

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

                // Attempt to reconnect
                if (!Connect())
                {
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

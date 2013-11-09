using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using Dache.CacheHost.Storage;
using Dache.Core.Interfaces;
using Dache.Core.Routing;

namespace Dache.CacheHost.Communication
{
    /// <summary>
    /// The server for client to cache communication.
    /// </summary>
    public class CacheHostServer : ICacheHostContract, IRunnable
    {
        // The cache server
        private readonly Socket _server = null;
        // The socket async event args pool
        private readonly Pool<SocketAsyncEventArgs> _socketAsyncEventArgsPool = null;
        // The buffer manager
        private readonly BufferManager _bufferManager = null;
        // Read, write (don't alloc buffer space for accepts)
        const int _opsToPreAllocate = 2;
        // The total number of clients connected to the server 
        private int _currentlyConnectedClients = 0;
        // The maximum number of simultaneous connections
        private readonly int _maximumConnections = 0;
        // The semaphore that enforces the maximum numbers of simultaneous connections
        private readonly Semaphore _maxConnectionsSemaphore;
        // The receive buffer size
        private readonly int _receiveBufferSize = 0;

        // The connection receiver cancellation token source
        private readonly CancellationTokenSource _connectionReceiverCancellationTokenSource = new CancellationTokenSource();
        // The default cache item policy
        private static readonly CacheItemPolicy _defaultCacheItemPolicy = new CacheItemPolicy();
        // The communication encoding
        public static readonly Encoding CommunicationEncoding = Encoding.UTF8;
        // The communication protocol control bytes default - 4 little endian bytes for message length + 4 little endian bytes for thread id + 1 control byte for message type
        private static readonly byte[] _controlBytesDefault = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        // The byte that represents a space
        private static readonly byte _spaceByte = CommunicationEncoding.GetBytes(" ")[0];
        // The absolute expiration format
        private const string _absoluteExpirationFormat = "yyMMddhhmmss";

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="maximumConnections">The maximum number of simultaneous connections.</param>
        /// <param name="receiveBufferSize">The buffer size to use for each socket I/O operation.</param>
        public CacheHostServer(int port, int maximumConnections, int receiveBufferSize)
        {
            // Set maximum connections and receive buffer size
            _maximumConnections = maximumConnections;
            _maxConnectionsSemaphore = new Semaphore(maximumConnections, maximumConnections);
            _receiveBufferSize = receiveBufferSize;

            // Initialize the socket async event args pool
            _socketAsyncEventArgsPool = new Pool<SocketAsyncEventArgs>(maximumConnections, () => new SocketAsyncEventArgs());

            // Allocate buffers such that the maximum number of sockets can have one outstanding read and write posted to the socket simultaneously
            _bufferManager = BufferManager.CreateBufferManager(receiveBufferSize * maximumConnections * _opsToPreAllocate, receiveBufferSize);

            // Preallocate pool of SocketAsyncEventArgs objects
            SocketAsyncEventArgs readWriteEventArg = null;
            for (int i = 0; i < maximumConnections; i++)
            {
                // Pre-allocate a set of reusable SocketAsyncEventArgs
                readWriteEventArg = new SocketAsyncEventArgs();
                readWriteEventArg.Completed += IO_Completed;
                readWriteEventArg.UserToken = new DacheProtocolHelper.StateObject(_receiveBufferSize);

                // Assign a byte buffer from the buffer pool to the SocketAsyncEventArg object
                readWriteEventArg.SetBuffer(_bufferManager.TakeBuffer(_receiveBufferSize), 0, _receiveBufferSize);

                // add SocketAsyncEventArg to the pool
                _socketAsyncEventArgsPool.Push(readWriteEventArg);
            }

            // Establish the endpoint for the socket
            var ipHostInfo = Dns.GetHostEntry("localhost");
            var ipAddress = ipHostInfo.AddressList.First(i => i.AddressFamily == AddressFamily.InterNetwork);
            var localEndPoint = new IPEndPoint(ipAddress, port);

            // Create the TCP/IP socket
            _server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            // Disable the Nagle algorithm
            _server.NoDelay = true;
            // Bind to endpoint
            _server.Bind(localEndPoint);
        }

        /// <summary>
        /// Begins an operation to accept a connection request from the client  
        /// </summary> 
        /// <param name="acceptEventArg">The context object to use when issuing the accept operation on the server's listening socket.</param> 
        public void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += AcceptEventArg_Completed;
            }
            else
            {
                // Socket must be cleared since the context object is being reused
                acceptEventArg.AcceptSocket = null;
            }

            _maxConnectionsSemaphore.WaitOne();
            bool willRaiseEvent = _server.AcceptAsync(acceptEventArg);
            if (!willRaiseEvent)
            {
                ProcessAccept(acceptEventArg);
            }
        }

        /// <summary>
        /// This method is the callback method associated with Socket.AcceptAsync operations and is invoked when an accept operation is complete.
        /// </summary>
        private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Interlocked.Increment(ref _currentlyConnectedClients);

            // Get the socket for the accepted client connection and put it into the ReadEventArg object user token
            var readEventArgs = _socketAsyncEventArgsPool.Pop();
            ((DacheProtocolHelper.StateObject)readEventArgs.UserToken).WorkSocket = e.AcceptSocket;
            // Turn off Nagle algorithm
            e.AcceptSocket.NoDelay = true;

            // As soon as the client is connected, post a receive to the connection
            bool willRaiseEvent = e.AcceptSocket.ReceiveAsync(readEventArgs);
            if (!willRaiseEvent)
            {
                ProcessReceive(readEventArgs);
            }

            // Accept the next connection request
            StartAccept(e);
        }

        /// <summary>
        /// This method is called whenever a receive or send operation is completed on a socket.
        /// </summary>
        /// <param name="e">SocketAsyncEventArg associated with the completed operation.</param>
        private void IO_Completed(object sender, SocketAsyncEventArgs e)
        {
            // determine which type of operation just completed and call the associated handler 
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous receive operation completes. If the remote host closed the connection, then the socket is closed.
        /// If data was received then the data is processed.
        /// </summary>
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            // check if the remote host closed the connection
            var state = (DacheProtocolHelper.StateObject)e.UserToken;

            // Increment the count of the total bytes received by the server
            // Interlocked.Add(ref _totalBytesRead, e.BytesTransferred);

            // Check for errors
            if (e.SocketError != SocketError.Success)
            {
                CloseClientSocket(e);
            }

            var handler = state.WorkSocket;
            int bytesRead = 0;

            // Read data
            if (state.TotalBytesToRead != 0 && (bytesRead = e.BytesTransferred) > 0 && e.SocketError == SocketError.Success)
            {
                // Append to buffer
                e.SetBuffer(e.Offset, e.BytesTransferred);

                // Check if we need to decode little endian
                if (state.TotalBytesToRead == -1)
                {
                    // Parse out control bytes
                    var strippedBuffer = e.Buffer.RemoveControlByteValues(out state.TotalBytesToRead, out state.ThreadId, out state.MessageType);
                    // Take control bytes off of bytes read
                    bytesRead -= _controlBytesDefault.Length;

                    // Set data
                    state.Data = DacheProtocolHelper.Combine(state.Data, state.Data.Length, strippedBuffer, bytesRead);
                }
                else
                {
                    state.Data = DacheProtocolHelper.Combine(state.Data, state.Data.Length, state.Buffer, bytesRead);
                }

                // Set total bytes read
                state.TotalBytesToRead -= bytesRead;

                if (state.TotalBytesToRead > 0)
                {
                    // Read the next block of data send from the client 
                    bool willRaiseEvent = handler.ReceiveAsync(e);
                    if (!willRaiseEvent)
                    {
                        ProcessReceive(e);
                    }
                    return;
                }
            }

            // Get the command bytes
            var commandBytes = state.Data;
            // Parse command from bytes
            var command = CommunicationEncoding.GetString(commandBytes);
            // Process the command
            byte[] commandResult = null;
            ProcessCommand(command, state.ThreadId, state.MessageType, handler, out commandResult);

            if (commandResult != null)
            {
                // Send response
                e.SetBuffer(commandResult, 0, commandResult.Length);
                bool willRaiseEvent = handler.SendAsync(e);
                if (!willRaiseEvent)
                {
                    ProcessSend(e);
                }
            }
            else
            {
                // Free the SocketAsyncEventArg so they can be reused by another client
                _socketAsyncEventArgsPool.Push(e);
            }
        }

        /// <summary>
        /// This method is invoked when an asynchronous send operation completes. The method issues another receive on the socket to read any additional data sent from the client.
        /// </summary>
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            // Free the SocketAsyncEventArg so they can be reused by another client
            _socketAsyncEventArgsPool.Push(e);
        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            var state = (DacheProtocolHelper.StateObject)e.UserToken;

            // Close the socket associated with the client 
            try
            {
                state.WorkSocket.Shutdown(SocketShutdown.Send);
            }
            // Throws if client process has already closed 
            catch
            {
                // ignore
            }

            state.WorkSocket.Close();

            // Decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref _currentlyConnectedClients);
            _maxConnectionsSemaphore.Release();

            // Free the SocketAsyncEventArg so they can be reused by another client
            _socketAsyncEventArgsPool.Push(e);
        }
         
        private void ProcessCommand(string command, int threadId, DacheProtocolHelper.MessageType messageType, Socket handler, out byte[] commandResult)
        {
            string[] commandParts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> cacheKeys = null;
            IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndObjects = null;
            List<byte[]> results = null;
            var absoluteExpiration = DateTimeOffset.MinValue;
            int slidingExpiration = 0;
            commandResult = null;

            switch (messageType)
            {
                case DacheProtocolHelper.MessageType.Literal:
                {
                    // Sanitize the command
                    if (commandParts.Length != 2)
                    {
                        return;
                    }

                    // The only command with no delimiter is get-tag so do that
                    var tagName = commandParts[1];
                    results = GetTagged(tagName);
                    // Structure the results for sending
                    using (var memoryStream = new MemoryStream())
                    {
                        // Write default control bytes - will be replaced later
                        memoryStream.Write(_controlBytesDefault);
                        foreach (var result in results)
                        {
                            memoryStream.WriteSpace();
                            memoryStream.WriteBase64(result);
                        }
                        commandResult = memoryStream.ToArray();
                    }

                    // Set control bytes
                    commandResult.SetControlBytes(threadId, DacheProtocolHelper.MessageType.RepeatingCacheObjects);

                    break;
                }
                case DacheProtocolHelper.MessageType.RepeatingCacheKeys:
                {
                    // Determine command
                    if (command.StartsWith("get", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return;
                        }

                        cacheKeys = commandParts.Skip(1).ToList();
                        results = Get(cacheKeys);
                        // Structure the results for sending
                        using (var memoryStream = new MemoryStream())
                        {
                            // Write default control bytes - will be replaced later
                            memoryStream.Write(_controlBytesDefault);
                            foreach (var result in results)
                            {
                                memoryStream.WriteSpace();
                                memoryStream.WriteBase64(result);
                            }
                            commandResult = memoryStream.ToArray();
                        }

                        // Set control bytes
                        commandResult.SetControlBytes(threadId, DacheProtocolHelper.MessageType.RepeatingCacheObjects);
                    }
                    else if (command.StartsWith("del-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return;
                        }

                        cacheKeys = commandParts.Skip(1).ToList();

                        foreach (var cacheKey in cacheKeys)
                        {
                            RemoveTagged(cacheKey);
                        }
                    }
                    else if (command.StartsWith("del", StringComparison.OrdinalIgnoreCase))
                    {
                        // Sanitize the command
                        if (commandParts.Length < 2)
                        {
                            return;
                        }

                        cacheKeys = commandParts.Skip(1).ToList();
                        Remove(cacheKeys);
                    }

                    break;
                }
                case DacheProtocolHelper.MessageType.RepeatingCacheKeysAndObjects:
                {
                    // Determine command
                    if (command.StartsWith("set-tag-intern", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only one method, so call it
                        if (commandParts.Length == 2)
                        {
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                            AddOrUpdateTaggedInterned(cacheKeysAndObjects, commandParts[1]);
                        }
                    }
                    else if (command.StartsWith("set-intern", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only one method, so call it
                        cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 1);
                        AddOrUpdateInterned(cacheKeysAndObjects);
                    }
                    else if (command.StartsWith("set-tag", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check whether we have absolute or sliding options
                        if (commandParts.Length % 2 != 0)
                        {
                            // Regular set
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                            AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1]);
                        }
                        else
                        {
                            // Get absolute or sliding expiration
                            if (DateTimeOffset.TryParseExact(commandParts[2], _absoluteExpirationFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out absoluteExpiration))
                            {
                                // absolute expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], absoluteExpiration);
                            }
                            else if (int.TryParse(commandParts[2], out slidingExpiration))
                            {
                                // sliding expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdateTagged(cacheKeysAndObjects, commandParts[1], TimeSpan.FromSeconds(slidingExpiration));
                            }
                            else
                            {
                                // Neither worked, so it's a bad message
                                return;
                            }
                        }
                    }
                    else if (command.StartsWith("set", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check whether we have absolute or sliding options
                        if (commandParts.Length % 2 != 0)
                        {
                            // Regular set
                            cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 1);
                            AddOrUpdate(cacheKeysAndObjects);
                        }
                        else
                        {
                            // Get absolute or sliding expiration
                            if (DateTimeOffset.TryParseExact(commandParts[1], _absoluteExpirationFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out absoluteExpiration))
                            {
                                // absolute expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdate(cacheKeysAndObjects, absoluteExpiration);
                            }
                            else if (int.TryParse(commandParts[1], out slidingExpiration))
                            {
                                // sliding expiration
                                cacheKeysAndObjects = ParseCacheKeysAndObjects(commandParts, 2);
                                AddOrUpdate(cacheKeysAndObjects, TimeSpan.FromSeconds(slidingExpiration));
                            }
                            else
                            {
                                // Neither worked, so it's a bad message
                                return;
                            }
                        }
                    }

                    break;
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, byte[]>> ParseCacheKeysAndObjects(string[] commandParts, int startIndex)
        {
            // Regular set
            var cacheKeysAndObjects = new List<KeyValuePair<string, byte[]>>(commandParts.Length / 2);
            for (int i = startIndex; i < commandParts.Length; i = i + 2)
            {
                cacheKeysAndObjects.Add(new KeyValuePair<string, byte[]>(commandParts[i], Convert.FromBase64String(commandParts[i + 1])));
            }
            return cacheKeysAndObjects;
        }

        /// <summary>
        /// Starts the cache server.
        /// </summary>
        public void Start()
        {
            // Listen for connections with a backlog of 100
            _server.Listen(100);
            
            // Post accept on the listening socket
            StartAccept(null);
        }

        /// <summary>
        /// Stops the cache server.
        /// </summary>
        public void Stop()
        {
            // Issue cancellation to connection receiver thread
            _connectionReceiverCancellationTokenSource.Cancel();
            
            // Shutdown and close the server socket
            try
            {
                _server.Shutdown(SocketShutdown.Both);
                _server.Close();
            }
            catch
            {
                try
                {
                    _server.Close();
                }
                catch
                {

                }
            }
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
                return null;
            }

            // Try to get value
            return MemCacheContainer.Instance.Get(cacheKey);
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
                return null;
            }

            var result = new List<byte[]>(10);

            // Iterate all cache keys
            foreach (var cacheKey in cacheKeys)
            {
                // Sanitize
                if (string.IsNullOrWhiteSpace(cacheKey))
                {
                    // Skip
                    continue;
                }

                // Try to get value
                var getResult = MemCacheContainer.Instance.Get(cacheKey);
                if (getResult != null)
                {
                    result.Add(getResult);
                }
            }

            // Return the result
            return result;
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
                return null;
            }

            // Compile a list of the serialized objects
            List<byte[]> result = new List<byte[]>(10);

            // Get the values
            var cacheKeys = TagRoutingTable.Instance.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    var cacheValue = MemCacheContainer.Instance.Get(cacheKey);
                    if (cacheValue == null)
                    {
                        continue;
                    }

                    result.Add(cacheValue);
                }
            }

            // Return the result
            return result;
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject)
        {
            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, _defaultCacheItemPolicy);
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, DateTimeOffset absoluteExpiration)
        {
            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = absoluteExpiration
            };
            
            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
        }

        /// <summary>
        /// Adds or updates a serialized object in the cache at the given cache key.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="serializedObject">The serialized object.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdate(string cacheKey, byte[] serializedObject, TimeSpan slidingExpiration)
        {
            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                SlidingExpiration = slidingExpiration
            };

            // Place object in cache
            MemCacheContainer.Instance.Add(cacheKey, serializedObject, cacheItemPolicy);
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
            // Place object in cache
            MemCacheContainer.Instance.AddInterned(cacheKey, serializedObject);
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

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value);
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

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, absoluteExpiration);
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

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdate(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, slidingExpiration);
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

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateInterned(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value);
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
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKey, serializedObject);
                return;
            }

            AddOrUpdate(cacheKey, serializedObject);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
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
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKey, serializedObject, absoluteExpiration);
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                AbsoluteExpiration = absoluteExpiration
            };

            AddOrUpdate(cacheKey, serializedObject, absoluteExpiration);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
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
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKey, serializedObject, slidingExpiration);
                return;
            }

            // Define the cache item policy
            var cacheItemPolicy = new CacheItemPolicy
            {
                SlidingExpiration = slidingExpiration
            };

            AddOrUpdate(cacheKey, serializedObject, slidingExpiration);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
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
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdateInterned(cacheKey, serializedObject);
                return;
            }

            AddOrUpdateInterned(cacheKey, serializedObject);

            // Add to the local tag routing table
            TagRoutingTable.Instance.AddOrUpdate(cacheKey, tagName);
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
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName);
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
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects, absoluteExpiration);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName, absoluteExpiration);
            }
        }

        /// <summary>
        /// Adds or updates the serialized objects in the cache at the given cache keys.
        /// </summary>
        /// <param name="cacheKeysAndSerializedObjects">The cache keys and associated serialized objects.</param>
        /// <param name="absoluteExpiration">The absolute expiration.</param>
        /// <param name="tagName">The tag name.</param>
        /// <param name="slidingExpiration">The sliding expiration.</param>
        public void AddOrUpdateTagged(IEnumerable<KeyValuePair<string, byte[]>> cacheKeysAndSerializedObjects, string tagName, TimeSpan slidingExpiration)
        {
            // Sanitize
            if (cacheKeysAndSerializedObjects == null)
            {
                throw new ArgumentNullException("cacheKeysAndSerializedObjects");
            }
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects, slidingExpiration);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName, slidingExpiration);
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
            if (string.IsNullOrWhiteSpace(tagName))
            {
                // If they didn't send a tag name ignore it
                AddOrUpdate(cacheKeysAndSerializedObjects);
                return;
            }

            // Iterate all cache keys and associated serialized objects
            foreach (var cacheKeysAndSerializedObjectKvp in cacheKeysAndSerializedObjects)
            {
                AddOrUpdateTagged(cacheKeysAndSerializedObjectKvp.Key, cacheKeysAndSerializedObjectKvp.Value, tagName);
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
                return;
            }

            // Remove object from cache
            MemCacheContainer.Instance.Remove(cacheKey);
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

            // Iterate all cache keys
            foreach (var cacheKey in cacheKeys)
            {
                Remove(cacheKey);
            }
        }

        /// <summary>
        /// Removes all serialized objects associated with the given tag name.
        /// </summary>
        /// <param name="tagName">The tag name.</param>
        public void RemoveTagged(string tagName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return;
            }

            // Remove them all
            var cacheKeys = TagRoutingTable.Instance.GetTaggedCacheKeys(tagName);
            if (cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    MemCacheContainer.Instance.Remove(cacheKey);
                }
            }
        }
    }
}

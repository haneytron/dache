using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Dache.CacheHost.Communication
{
    /// <summary>
    /// Makes sockets rock a lot harder than they do out-of-the-box. Easy, extremely efficient, scalable and fast methods for intuitive client-server communication.
    /// This class can operate as a client or server.
    /// </summary>
    public sealed class SocketRocker : IDisposable
    {
        // The function that creates a new socket
        private readonly Func<Socket> _socketFunc = null;
        // The currently used socket
        private Socket _socket = null;
        // The message buffer size to use for send/receive
        private readonly int _messageBufferSize = 0;
        // The maximum connections to allow to use the socket simultaneously
        private readonly int _maximumConnections = 0;
        // The semaphore that enforces the maximum numbers of simultaneous connections
        private readonly Semaphore _maxConnectionsSemaphore;
        // Whether or not to use the Nagle algorithm
        private readonly bool _useNagleAlgorithm = false;

        // The queue of received messages (server only)
        private readonly Queue<KeyValuePair<int, byte[]>> _receivedMessages = null;
        // The number of currently connected clients
        private int _currentlyConnectedClients = 0;
        // Whether or not a connection currently exists
        private volatile bool _isDoingSomething = false;
        // Whether or not to use the multiplexer.
        private bool _useClientMultiplexer = false;

        // The client multiplexer
        private readonly Dictionary<int, KeyValuePair<MessageState, ManualResetEvent>> _clientMultiplexer = null;
        // The client multiplexer reader writer lock
        private readonly ReaderWriterLockSlim _clientMultiplexerLock = new ReaderWriterLockSlim();
        // The pool of manual reset events
        private readonly Pool<ManualResetEvent> _manualResetEventPool = null;
        // The pool of message states
        private readonly Pool<MessageState> _messageStatePool = null;

        // The control bytes placeholder - the first 4 bytes are little endian message length, the last 4 are thread id
        private static readonly byte[] _controlBytesPlaceholder = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="socketFunc">The function that creates a new socket. Use this to specify your socket constructor and initialize settings.</param>
        /// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
        /// <param name="maximumConnections">The maximum connections to allow to use the socket simultaneously.</param>
        /// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
        public SocketRocker(Func<Socket> socketFunc, int messageBufferSize, int maximumConnections, bool useNagleAlgorithm)
        {
            // Sanitize
            if (socketFunc == null)
            {
                throw new ArgumentNullException("socketFunc");
            }
            if (messageBufferSize < 256)
            {
                throw new ArgumentException("must be >= 256", "messageBufferSize");
            }
            if (maximumConnections <= 0)
            {
                throw new ArgumentException("must be > 0", "maximumConnections");
            }

            _socketFunc = socketFunc;
            _messageBufferSize = messageBufferSize;
            _maximumConnections = maximumConnections;
            _maxConnectionsSemaphore = new Semaphore(maximumConnections, maximumConnections);
            _useNagleAlgorithm = useNagleAlgorithm;

            _receivedMessages = new Queue<KeyValuePair<int, byte[]>>(maximumConnections);

            // Create the pools
            _messageStatePool = new Pool<MessageState>(maximumConnections, () => new MessageState(), messageState =>
            {
                if (messageState.Data != null)
                {
                    messageState.Data.Dispose();
                }
                messageState.Handler = null;
                messageState.ThreadId = -1;
                messageState.TotalBytesToRead = -1;
            });
            _manualResetEventPool = new Pool<ManualResetEvent>(maximumConnections, () => new ManualResetEvent(false), manualResetEvent => manualResetEvent.Reset());
            // Populate the pools
            for (int i = 0; i < maximumConnections; i++)
            {
                _messageStatePool.Push(new MessageState { Buffer = new byte[messageBufferSize] });
                _manualResetEventPool.Push(new ManualResetEvent(false));
            }
        }

        /// <summary>
        /// Gets the currently connected client count.
        /// </summary>
        public int CurrentlyConnectedClients
        {
            get
            {
                return _currentlyConnectedClients;
            }
        }

        /// <summary>
        /// Connects to an endpoint. Once this is called, you must call Close before calling Connect or Listen again.
        /// </summary>
        /// <param name="endPoint">The endpoint.</param>
        public void Connect(EndPoint endPoint)
        {
            if (_isDoingSomething)
            {
                throw new InvalidOperationException("socket is already in use");
            }

            _isDoingSomething = true;
            _useClientMultiplexer = true;

            // Create socket
            _socket = _socketFunc();
            // Set appropriate nagle
            _socket.NoDelay = !_useNagleAlgorithm;

            // Post a connect to the socket synchronously
            _socket.Connect(endPoint);

            // Get a message state from the pool
            var messageState = _messageStatePool.Pop();
            messageState.Data = new MemoryStream();
            messageState.Handler = _socket;

            // Post a receive to the socket as the client will be continuously receiving messages to be pushed to the queue
            _socket.BeginReceive(messageState.Buffer, 0, messageState.Buffer.Length, 0, ReceiveCallback, messageState);
        }

        /// <summary>
        /// Begin listening for incoming connections. Once this is called, you must call Close before calling Connect or Listen again.
        /// </summary>
        /// <param name="localEndpoint">The local endpoint to listen on.</param>
        public void Listen(EndPoint localEndpoint)
        {
            if (_isDoingSomething)
            {
                throw new InvalidOperationException("socket is already in use");
            }

            _isDoingSomething = true;

            // Create socket
            _socket = _socketFunc();

            _socket.Bind(localEndpoint);
            _socket.Listen(_maximumConnections);

            // Post accept on the listening socket
            _socket.BeginAccept(AcceptCallback, null);
        }

        /// <summary>
        /// Closes the connection. Once this is called, you can call Connect or Listen again to start a new socket connection.
        /// </summary>
        public void Close()
        {
            // Close the socket
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Ignore
            }

            _socket.Close();
            // No longer doing something
            _isDoingSomething = false;
        }

        // TODO: implement
        private void HandleError(Socket socket)
        {
            // Close the socket
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Ignore
            }

            socket.Close();

            // Release one from the semaphore
            _maxConnectionsSemaphore.Release();

            // Decrement the counter keeping track of the total number of clients connected to the server
            Interlocked.Decrement(ref _currentlyConnectedClients);
        }

        private void AcceptCallback(IAsyncResult asyncResult)
        {
            Interlocked.Increment(ref _currentlyConnectedClients);

            // Get the client handler socket
            Socket handler = _socket.EndAccept(asyncResult);
            handler.NoDelay = !_useNagleAlgorithm;

            // Post accept on the listening socket
            _socket.BeginAccept(AcceptCallback, null);

            // Do not proceed until we have room to do so
            _maxConnectionsSemaphore.WaitOne();

            // Get message state
            var messageState = _messageStatePool.Pop();
            messageState.Data = new MemoryStream();
            messageState.Handler = handler;

            // Post receive on the handler socket
            handler.BeginReceive(messageState.Buffer, 0, messageState.Buffer.Length, 0, ReceiveCallback, messageState);
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            // Get the message state
            MessageState messageState = (MessageState)asyncResult.AsyncState;
            // TODO: check error code

            // Read the data
            int bytesRead = messageState.Handler.EndReceive(asyncResult);

            if (bytesRead > 0)
            {
                ProcessReceivedMessage(messageState, 0, bytesRead);
            }
        }

        private void ProcessReceivedMessage(MessageState messageState, int currentOffset, int bytesRead)
        {
            bool isFirstPartOfFrame = false;

            // Check if we need to get our control byte values
            if (messageState.TotalBytesToRead == -1)
            {
                isFirstPartOfFrame = true;

                // We do, see if we have enough bytes received to get them
                if (currentOffset + _controlBytesPlaceholder.Length >= bytesRead)
                {
                    // We don't yet have enough bytes to read the control bytes, so get more bytes
                    int bytesOfControlReceived = bytesRead - currentOffset;
                    int neededBytes = _controlBytesPlaceholder.Length - bytesOfControlReceived;
                    byte[] controlBytesBuffer = new byte[_controlBytesPlaceholder.Length];
                    // Populate received control bytes
                    for (int i = 0; i < bytesOfControlReceived; i++)
                    {
                        controlBytesBuffer[i] = messageState.Buffer[currentOffset + i];
                    }
                    int receivedControlBytes = 0;
                    while (neededBytes > 0)
                    {
                        // Receive just the needed bytes
                        receivedControlBytes = messageState.Handler.Receive(controlBytesBuffer, bytesOfControlReceived + receivedControlBytes, controlBytesBuffer.Length - bytesOfControlReceived + receivedControlBytes, 0);
                        neededBytes -= receivedControlBytes;
                    }
                    // Now we have the needed control bytes, parse out control bytes
                    ExtractControlBytes(controlBytesBuffer, 0, out messageState.TotalBytesToRead, out messageState.ThreadId);
                    // We know this is all we've received, so receive more data
                    messageState.Handler.BeginReceive(messageState.Buffer, 0, messageState.Buffer.Length, 0, ReceiveCallback, messageState);
                    return;
                }

                // Parse out control bytes
                ExtractControlBytes(messageState.Buffer, currentOffset, out messageState.TotalBytesToRead, out messageState.ThreadId);
                // Offset the index by the control bytes
                currentOffset += _controlBytesPlaceholder.Length;
                // Take control bytes off of bytes read
                bytesRead -= _controlBytesPlaceholder.Length;
            }

            int numberOfBytesToRead = Math.Min(bytesRead, messageState.TotalBytesToRead);
            messageState.Data.Write(messageState.Buffer, currentOffset, numberOfBytesToRead);

            // Set total bytes read
            int originalTotalBytesToRead = messageState.TotalBytesToRead + (isFirstPartOfFrame ? _controlBytesPlaceholder.Length : 0);
            messageState.TotalBytesToRead -= bytesRead;

            // Check if we're done
            if (messageState.TotalBytesToRead == 0)
            {
                // Done, add to complete received messages
                CompleteMessage(messageState.ThreadId, messageState.Data.ToArray());
                // Get new state for the next message but transfer over handler
                Socket handler = messageState.Handler;
                _messageStatePool.Push(messageState);
                messageState = _messageStatePool.Pop();
                messageState.Data = new MemoryStream();
                messageState.Handler = handler;

                // Receive more data
                messageState.Handler.BeginReceive(messageState.Buffer, 0, messageState.Buffer.Length, 0, ReceiveCallback, messageState);
                return;
            }

            // Not done, see if we need to get more data
            if (messageState.TotalBytesToRead > 0)
            {
                // Receive more data
                messageState.Handler.BeginReceive(messageState.Buffer, 0, messageState.Buffer.Length, 0, ReceiveCallback, messageState);
                return;
            }

            // Check if we have an overlapping message frame in our message AKA if the bytesRead was larger than the total bytes to read
            if (bytesRead > messageState.TotalBytesToRead)
            {
                // This message is completed, add to complete received messages
                CompleteMessage(messageState.ThreadId, messageState.Data.ToArray());
                // Get new state for the next message but transfer over handler
                Socket handler = messageState.Handler;
                _messageStatePool.Push(messageState);
                messageState = _messageStatePool.Pop();
                messageState.Data = new MemoryStream();
                messageState.Handler = handler;

                // Get the number of bytes remaining to be read
                int bytesRemaining = bytesRead - originalTotalBytesToRead;

                // Now we have the next message, so recursively process it
                ProcessReceivedMessage(messageState, originalTotalBytesToRead, bytesRemaining);
                return;
            }
        }

        /// <summary>
        /// Sends a message to the server.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="registerForResponse">Whether or not to register for a response. Set this false if you don't care about the response. If you do care, set this to true and then call ClientReceive.</param>
        public void ClientSend(byte[] message, bool registerForResponse)
        {
            if (!_useClientMultiplexer)
            {
                throw new InvalidOperationException("Cannot call ClientSend when listening for connections");
            }

            // Sanitize
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            int threadId = Thread.CurrentThread.ManagedThreadId;

            // Check if we need to register with the multiplexer
            if (registerForResponse)
            {
                EnrollMultiplexer(threadId);
            }

            // Create room for the control bytes
            var messageWithControlBytes = new byte[message.Length + _controlBytesPlaceholder.Length];
            Buffer.BlockCopy(message, 0, messageWithControlBytes, _controlBytesPlaceholder.Length, message.Length);
            // Set the control bytes on the message
            SetControlBytes(messageWithControlBytes, threadId);

            // Do the send
            _socket.BeginSend(messageWithControlBytes, 0, messageWithControlBytes.Length, 0, SendCallback, null);
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            // Complete the send
            _socket.EndSend(asyncResult);
        }

        /// <summary>
        /// Receives a message from the server.
        /// </summary>
        /// <returns>The message.</returns>
        public byte[] ClientReceive()
        {
            if (!_useClientMultiplexer)
            {
                throw new InvalidOperationException("Cannot call ClientReceive when listening for connections");
            }

            // Get this thread's message state object and manual reset event
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var messageStateAndManualResetEvent = GetMultiplexerValue(threadId);

            // Wait for our message to go ahead from the receive callback
            messageStateAndManualResetEvent.Value.WaitOne();

            // Now get the command string
            var result = messageStateAndManualResetEvent.Key.Data.ToArray();

            // Finally remove the thread from the multiplexer
            UnenrollMultiplexer(threadId);

            return result;
        }

        /// <summary>
        /// Receives a message from the client. This method will block until a message becomes available, and is also thread safe.
        /// </summary>
        /// <param name="threadId">The thread ID. Must be passed to ServerSend in order to reply to a client.</param>
        /// <returns>The message.</returns>
        public byte[] ServerReceive(out int threadId)
        {
            if (_useClientMultiplexer)
            {
                throw new InvalidOperationException("Cannot call ServerReceive when connected to a remote server");
            }

            return GetCompletedMessage(out threadId);
        }

        /// <summary>
        /// Sends a message back to the client.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="threadId">The thread ID obtained from ServerReceive.</param>
        public void ServerSend(byte[] message, int threadId)
        {
            if (!_useClientMultiplexer)
            {
                throw new InvalidOperationException("Cannot call ServerSend when connected to a remote server");
            }

            // Sanitize
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }

            // Create room for the control bytes
            var messageWithControlBytes = new byte[message.Length + _controlBytesPlaceholder.Length];
            Buffer.BlockCopy(message, 0, messageWithControlBytes, _controlBytesPlaceholder.Length, message.Length);
            // Set the control bytes on the message
            SetControlBytes(messageWithControlBytes, threadId);

            // Do the send
            _socket.BeginSend(messageWithControlBytes, 0, messageWithControlBytes.Length, 0, SendCallback, null);
        }

        /// <summary>
        /// Disposes the class.
        /// </summary>
        public void Dispose()
        {
            // Close/dispose the socket
            _socket.Close();
        }

        private void CompleteMessage(int threadId, byte[] message)
        {
            lock (_receivedMessages)
            {
                _receivedMessages.Enqueue(new KeyValuePair<int, byte[]>(threadId, message));
            }
        }

        private byte[] GetCompletedMessage(out int threadId)
        {
            while (true)
            {
                if (_receivedMessages.Count != 0)
                {
                    lock (_receivedMessages)
                    {
                        // Double lock check
                        if (_receivedMessages.Count != 0)
                        {
                            var result = _receivedMessages.Dequeue();
                            threadId = result.Key;
                            return result.Value;
                        }
                    }
                }

                // Sleep and try again
                Thread.Sleep(200);
            }
        }

        private class MessageState
        {
            public byte[] Buffer = null;
            public Socket Handler = null;
            public MemoryStream Data = null;
            public int ThreadId = -1;
            public int TotalBytesToRead = -1;
        }

        private static void SetControlBytes(byte[] buffer, int threadId)
        {
            var length = buffer.Length;
            // Set little endian message length
            buffer[0] = (byte)length;
            buffer[1] = (byte)((length >> 8) & 0xFF);
            buffer[2] = (byte)((length >> 16) & 0xFF);
            buffer[3] = (byte)((length >> 24) & 0xFF);
            // Set little endian thread id
            buffer[4] = (byte)threadId;
            buffer[5] = (byte)((threadId >> 8) & 0xFF);
            buffer[6] = (byte)((threadId >> 16) & 0xFF);
            buffer[7] = (byte)((threadId >> 24) & 0xFF);
        }

        private static void ExtractControlBytes(byte[] buffer, int offset, out int messageLength, out int threadId)
        {
            messageLength = (buffer[offset + 3] << 24) | (buffer[offset + 2] << 16) | (buffer[offset + 1] << 8) | buffer[offset + 0] - _controlBytesPlaceholder.Length;
            threadId = (buffer[offset + 7] << 24) | (buffer[offset + 6] << 16) | (buffer[offset + 5] << 8) | buffer[offset + 4];
        }

        private KeyValuePair<MessageState, ManualResetEvent> GetMultiplexerValue(int threadId)
        {
            KeyValuePair<MessageState, ManualResetEvent> messageStateAndManualResetEvent;
            _clientMultiplexerLock.EnterReadLock();
            try
            {
                // Get from multiplexer by thread ID
                if (!_clientMultiplexer.TryGetValue(threadId, out messageStateAndManualResetEvent))
                {
                    throw new Exception("FATAL: multiplexer was missing entry for Thread ID " + threadId);
                }

                return messageStateAndManualResetEvent;
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
                _clientMultiplexer.Add(threadId, new KeyValuePair<MessageState, ManualResetEvent>(_messageStatePool.Pop(), _manualResetEventPool.Pop()));
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
            KeyValuePair<MessageState, ManualResetEvent> messageStateAndManualResetEvent;
            _clientMultiplexerLock.EnterUpgradeableReadLock();
            try
            {
                // Get from multiplexer by thread ID
                if (!_clientMultiplexer.TryGetValue(threadId, out messageStateAndManualResetEvent))
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
            _messageStatePool.Push(messageStateAndManualResetEvent.Key);
            _manualResetEventPool.Push(messageStateAndManualResetEvent.Value);
        }

        private void SignalMultiplexer(int threadId)
        {
            KeyValuePair<MessageState, ManualResetEvent> messageStateAndManualResetEvent;
            _clientMultiplexerLock.EnterReadLock();
            try
            {
                // Get from multiplexer by thread ID
                if (!_clientMultiplexer.TryGetValue(threadId, out messageStateAndManualResetEvent))
                {
                    throw new Exception("FATAL: multiplexer was missing entry for Thread ID " + threadId);
                }

                messageStateAndManualResetEvent.Value.Set();
            }
            finally
            {
                _clientMultiplexerLock.ExitReadLock();
            }
        }
    }
}

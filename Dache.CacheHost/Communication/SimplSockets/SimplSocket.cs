using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dache.Core.Logging;

namespace SimplSockets
{
	/// <summary>
	/// Wraps sockets and provides intuitive, extremely efficient, scalable methods for client-server communication.
	/// </summary>
	public class SimplSocket : ISimplSocketClient, ISimplSocketServer, IDisposable
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
		private readonly Semaphore _maxConnectionsSemaphore = null;
		// Whether or not to use the Nagle algorithm
		private readonly bool _useNagleAlgorithm = false;

		// The receive buffer queue
		private readonly Dictionary<int, BlockingQueue<KeyValuePair<byte[], int>>> _receiveBufferQueue = null;
		// The receive buffer queue lock
		private readonly ReaderWriterLockSlim _receiveBufferQueueLock = new ReaderWriterLockSlim();

		// The number of currently connected clients
		private int _currentlyConnectedClients = 0;
		// Whether or not a connection currently exists
		private volatile bool _isDoingSomething = false;
		// Whether or not to use the multiplexer
		private bool _useClientMultiplexer = false;

		// The client multiplexer
		private readonly Dictionary<int, MultiplexerData> _clientMultiplexer = null;
		// The client multiplexer reader writer lock
		private readonly ReaderWriterLockSlim _clientMultiplexerLock = new ReaderWriterLockSlim();
		// The pool of manual reset events
		private readonly Pool<ManualResetEvent> _manualResetEventPool = null;
		// The pool of message states
		private readonly Pool<MessageState> _messageStatePool = null;
		// The pool of buffers
		private readonly Pool<byte[]> _bufferPool = null;
		// The pool of receive messages
		private readonly Pool<ReceivedMessage> _receiveMessagePool = null;

		// The control bytes placeholder - the first 4 bytes are little endian message length, the last 4 are thread id
		private static readonly byte[] _controlBytesPlaceholder = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 };

		// The logger
		private readonly ILogger _logger;

		/// <summary>
		/// Create a client.
		/// </summary>
		/// <param name="socketFunc">The function that creates a new socket. Use this to specify your socket constructor and initialize settings.</param>
		/// <param name="errorHandler">The error handler that is raised when a Socket error occurs.</param>
		/// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
		/// <param name="maximumConnections">The maximum connections to allow to use the socket simultaneously.</param>
		/// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
		public static ISimplSocketClient CreateClient(Func<Socket> socketFunc, EventHandler<SocketErrorArgs> errorHandler, int messageBufferSize, int maximumConnections, bool useNagleAlgorithm)
		{
			return new SimplSocket(socketFunc, errorHandler, messageBufferSize, maximumConnections, useNagleAlgorithm);
		}

		/// <summary>
		/// Create a server.
		/// </summary>
		/// <param name="socketFunc">The function that creates a new socket. Use this to specify your socket constructor and initialize settings.</param>
		/// <param name="errorHandler">The error handler that is raised when a Socket error occurs.</param>
		/// <param name="messageHandler">The message handler that handles incoming messages.</param>
		/// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
		/// <param name="maximumConnections">The maximum connections to allow to use the socket simultaneously.</param>
		/// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
		public static ISimplSocketServer CreateServer(Func<Socket> socketFunc, EventHandler<SocketErrorArgs> errorHandler, EventHandler<MessageReceivedArgs> messageHandler, 
			int messageBufferSize, int maximumConnections, bool useNagleAlgorithm)
		{
			// Sanitize
			if (messageHandler == null)
			{
				throw new ArgumentNullException("messageHandler");
			}

			var simplSocket = new SimplSocket(socketFunc, errorHandler, messageBufferSize, maximumConnections, useNagleAlgorithm);
			simplSocket.MessageReceived += messageHandler;
			return simplSocket;
		}

		/// <summary>
		/// The private constructor - used to enforce factor-style instantiation.
		/// </summary>
		/// <param name="socketFunc">The function that creates a new socket. Use this to specify your socket constructor and initialize settings.</param>
		/// <param name="errorHandler">The error handler that is raised when a Socket error occurs.</param>
		/// <param name="messageBufferSize">The message buffer size to use for send/receive.</param>
		/// <param name="maximumConnections">The maximum connections to allow to use the socket simultaneously.</param>
		/// <param name="useNagleAlgorithm">Whether or not to use the Nagle algorithm.</param>
		private SimplSocket(Func<Socket> socketFunc, EventHandler<SocketErrorArgs> errorHandler, int messageBufferSize, int maximumConnections, bool useNagleAlgorithm)
		{
			// Sanitize
			if (socketFunc == null)
			{
				throw new ArgumentNullException("socketFunc");
			}
			if (errorHandler == null)
			{
				throw new ArgumentNullException("errorHandler");
			}
			if (messageBufferSize < 128)
			{
				throw new ArgumentException("must be >= 128", "messageBufferSize");
			}
			if (maximumConnections <= 0)
			{
				throw new ArgumentException("must be > 0", "maximumConnections");
			}

			_socketFunc = socketFunc;
			Error += errorHandler;
			_messageBufferSize = messageBufferSize;
			_maximumConnections = maximumConnections;
			_maxConnectionsSemaphore = new Semaphore(maximumConnections, maximumConnections);
			_useNagleAlgorithm = useNagleAlgorithm;

			_receiveBufferQueue = new Dictionary<int, BlockingQueue<KeyValuePair<byte[], int>>>(maximumConnections);

			// Initialize the client multiplexer
			_clientMultiplexer = new Dictionary<int, MultiplexerData>(maximumConnections);

			// Create the pools
			_messageStatePool = new Pool<MessageState>(maximumConnections, () => new MessageState(), messageState =>
			{
				if (messageState.Data != null)
				{
					messageState.Data.Dispose();
				}
				messageState.Buffer = null;
				messageState.Handler = null;
				messageState.ThreadId = -1;
				messageState.TotalBytesToRead = -1;
			});
			_manualResetEventPool = new Pool<ManualResetEvent>(maximumConnections, () => new ManualResetEvent(false), manualResetEvent => manualResetEvent.Reset());
			_bufferPool = new Pool<byte[]>(maximumConnections * 10, () => new byte[messageBufferSize], null);
			_receiveMessagePool = new Pool<ReceivedMessage>(maximumConnections, () => new ReceivedMessage(), receivedMessage =>
			{
				receivedMessage.Message = null;
				receivedMessage.Socket = null;
			});

			// Populate the pools
			for (int i = 0; i < maximumConnections; i++)
			{
				_messageStatePool.Push(new MessageState());
				_manualResetEventPool.Push(new ManualResetEvent(false));
				_bufferPool.Push(new byte[messageBufferSize]);
				_receiveMessagePool.Push(new ReceivedMessage());
			}

			// Load custom logging
			_logger = new FileLogger();
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
		/// Connects to an endpoint. Once this is called, you must call Close before calling Connect or Listen again. The errorHandler method 
		/// will not be called if the connection fails. Instead this method will return false.
		/// </summary>
		/// <param name="endPoint">The endpoint.</param>
		/// <returns>true if connection is successful, false otherwise.</returns>
		public bool Connect(EndPoint endPoint)
		{
			// Sanitize
			if (_isDoingSomething)
			{
				throw new InvalidOperationException("socket is already in use");
			}
			if (endPoint == null)
			{
				throw new ArgumentNullException("endPoint");
			}

			_isDoingSomething = true;
			_useClientMultiplexer = true;

			// Create socket
			_socket = _socketFunc();
			// Turn on or off Nagle algorithm
			_socket.NoDelay = !_useNagleAlgorithm;

			// Do not proceed until we have room to do so
			_maxConnectionsSemaphore.WaitOne();

			Interlocked.Increment(ref _currentlyConnectedClients);

			// Post a connect to the socket synchronously
			try
			{
				_socket.Connect(endPoint);
			}
			catch (SocketException ex)
			{
				_isDoingSomething = false;
				_logger.Error(ex);
				return false;
			}

			// Get a message state from the pool
			var messageState = _messageStatePool.Pop();
			messageState.Data = new MemoryStream();
			messageState.Handler = _socket;
			// Get a buffer from the buffer pool
			var buffer = _bufferPool.Pop();

			// Create receive queue for this client
			_receiveBufferQueueLock.EnterWriteLock();
			try
			{
				_receiveBufferQueue[messageState.Handler.GetHashCode()] = new BlockingQueue<KeyValuePair<byte[], int>>(_maximumConnections * 10);
			}
			finally
			{
				_receiveBufferQueueLock.ExitWriteLock();
			}

			// Post a receive to the socket as the client will be continuously receiving messages to be pushed to the queue
			_socket.BeginReceive(buffer, 0, buffer.Length, 0, ReceiveCallback, new KeyValuePair<MessageState, byte[]>(messageState, buffer));

			// Process all incoming messages
			Task.Factory.StartNew(() => ProcessReceivedMessage(messageState));

			return true;
		}

		/// <summary>
		/// Begin listening for incoming connections. Once this is called, you must call Close before calling Connect or Listen again.
		/// </summary>
		/// <param name="localEndpoint">The local endpoint to listen on.</param>
		public void Listen(EndPoint localEndpoint)
		{
			// Sanitize
			if (_isDoingSomething)
			{
				throw new InvalidOperationException("socket is already in use");
			}
			if (localEndpoint == null)
			{
				throw new ArgumentNullException("localEndpoint");
			}

			_isDoingSomething = true;

			// Create socket
			_socket = _socketFunc();

			try
			{
				_socket.Bind(localEndpoint);
				_socket.Listen(_maximumConnections);

				// Post accept on the listening socket
				_socket.BeginAccept(AcceptCallback, null);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(_socket, ex);
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
			}
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

		/// <summary>
		/// Sends a message to the server without waiting for a response (one-way communication).
		/// </summary>
		/// <param name="message">The message to send.</param>
		public void Send(byte[] message)
		{
			// Sanitize
			if (message == null)
			{
				throw new ArgumentNullException("message");
			}

			// Get the current thread ID
			int threadId = Thread.CurrentThread.ManagedThreadId;

			// Create room for the control bytes
			var messageWithControlBytes = new byte[message.Length + _controlBytesPlaceholder.Length];
			Buffer.BlockCopy(message, 0, messageWithControlBytes, _controlBytesPlaceholder.Length, message.Length);
			// Set the control bytes on the message
			SetControlBytes(messageWithControlBytes, threadId);

			// Do the send
			try
			{
				_socket.BeginSend(messageWithControlBytes, 0, messageWithControlBytes.Length, 0, SendCallback, _socket);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(_socket, ex);
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return;
			}
		}

		/// <summary>
		/// Sends a message to the server and receives the response to that message.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <returns>The response message.</returns>
		public byte[] SendReceive(byte[] message)
		{
			// Sanitize
			if (message == null)
			{
				throw new ArgumentNullException("message");
			}

			// Get the current thread ID
			int threadId = Thread.CurrentThread.ManagedThreadId;
			// Enroll in the multiplexer
			var multiplexerData = EnrollMultiplexer(threadId);

			// Create room for the control bytes
			var messageWithControlBytes = new byte[message.Length + _controlBytesPlaceholder.Length];
			Buffer.BlockCopy(message, 0, messageWithControlBytes, _controlBytesPlaceholder.Length, message.Length);
			// Set the control bytes on the message
			SetControlBytes(messageWithControlBytes, threadId);

			// Do the send
			try
			{
				_socket.BeginSend(messageWithControlBytes, 0, messageWithControlBytes.Length, 0, SendCallback, _socket);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(_socket, ex);
				return null;
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return null;
			}

			// Wait for our message to go ahead from the receive callback
			multiplexerData.ManualResetEvent.WaitOne();

			// Now get the command string
			var result = multiplexerData.Message;

			// Finally remove the thread from the multiplexer
			UnenrollMultiplexer(threadId);

			return result;
		}

		/// <summary>
		/// Sends a message back to the client.
		/// </summary>
		/// <param name="message">The reply message to send.</param>
		/// <param name="receivedMessage">The received message which is being replied to.</param>
		public void Reply(byte[] message, ReceivedMessage receivedMessage)
		{
			// Sanitize
			if (message == null)
			{
				throw new ArgumentNullException("message");
			}
			if (receivedMessage.Socket == null)
			{
				throw new ArgumentException("contains corrupted data", "receivedMessageState");
			}

			// Create room for the control bytes
			var messageWithControlBytes = new byte[message.Length + _controlBytesPlaceholder.Length];
			Buffer.BlockCopy(message, 0, messageWithControlBytes, _controlBytesPlaceholder.Length, message.Length);
			// Set the control bytes on the message
			SetControlBytes(messageWithControlBytes, receivedMessage.ThreadId);

			// Do the send to the appropriate client
			try
			{
				receivedMessage.Socket.BeginSend(messageWithControlBytes, 0, messageWithControlBytes.Length, 0, SendCallback, receivedMessage.Socket);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(receivedMessage.Socket, ex);
				return;
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return;
			}

			// Put received message back in the pool
			_receiveMessagePool.Push(receivedMessage);
		}

		/// <summary>
		/// An event that is fired whenever a message is received. Hook into this to process messages and potentially call Reply to send a response.
		/// </summary>
		public event EventHandler<MessageReceivedArgs> MessageReceived;

		/// <summary>
		/// An event that is fired whenever a socket communication error occurs.
		/// </summary>
		private event EventHandler<SocketErrorArgs> Error;

		/// <summary>
		/// Disposes the instance and frees unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			// Close/dispose the socket
			_socket.Close();
		}

		private void AcceptCallback(IAsyncResult asyncResult)
		{
			Interlocked.Increment(ref _currentlyConnectedClients);

			// Get the client handler socket
			Socket handler = null;
			try
			{
				handler = _socket.EndAccept(asyncResult);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(_socket, ex);
				return;
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return;
			}

			// Turn on or off Nagle algorithm
			handler.NoDelay = !_useNagleAlgorithm;

			// Post accept on the listening socket
			try
			{
				_socket.BeginAccept(AcceptCallback, null);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(_socket, ex);
				return;
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return;
			}

			// Do not proceed until we have room to do so
			_maxConnectionsSemaphore.WaitOne();

			// Get message state
			var messageState = _messageStatePool.Pop();
			messageState.Data = new MemoryStream();
			messageState.Handler = handler;

			// Post receive on the handler socket
			var buffer = _bufferPool.Pop();
			try
			{
				handler.BeginReceive(buffer, 0, buffer.Length, 0, ReceiveCallback, new KeyValuePair<MessageState, byte[]>(messageState, buffer));
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(handler, ex);
				return;
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return;
			}

			// Create receive queue for this client
			_receiveBufferQueueLock.EnterWriteLock();
			try
			{
				_receiveBufferQueue[messageState.Handler.GetHashCode()] = new BlockingQueue<KeyValuePair<byte[], int>>(_maximumConnections * 10);
			}
			finally
			{
				_receiveBufferQueueLock.ExitWriteLock();
			}

			// Process all incoming messages
			ProcessReceivedMessage(messageState);
		}

		private void SendCallback(IAsyncResult asyncResult)
		{
			// Get the socket to complete on
			Socket socket = (Socket)asyncResult.AsyncState;

			// Complete the send
			try
			{
				socket.EndSend(asyncResult);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(socket, ex);
				return;
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return;
			}
		}

		private void ReceiveCallback(IAsyncResult asyncResult)
		{
			// Get the message state and buffer
			var messageStateAndBuffer = (KeyValuePair<MessageState, byte[]>)asyncResult.AsyncState;
			MessageState messageState = messageStateAndBuffer.Key;
			byte[] buffer = messageStateAndBuffer.Value;
			int bytesRead = 0;

			// Read the data
			try
			{
				bytesRead = messageState.Handler.EndReceive(asyncResult);
			}
			catch (SocketException ex)
			{
				HandleCommunicationError(messageState.Handler, ex);
				return;
			}
			catch (ObjectDisposedException)
			{
				// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				return;
			}

			if (bytesRead > 0)
			{
				// Add buffer to queue
				BlockingQueue<KeyValuePair<byte[], int>> queue = null;
				_receiveBufferQueueLock.EnterReadLock();
				try
				{
					if (!_receiveBufferQueue.TryGetValue(messageState.Handler.GetHashCode(), out queue))
					{
						throw new Exception("FATAL: No receive queue created for current socket");
					}
				}
				finally
				{
					_receiveBufferQueueLock.ExitReadLock();
				}

				queue.Enqueue(new KeyValuePair<byte[], int>(buffer, bytesRead));

				// Post receive on the handler socket
				buffer = _bufferPool.Pop();
				try
				{
					messageState.Handler.BeginReceive(buffer, 0, buffer.Length, 0, ReceiveCallback, new KeyValuePair<MessageState, byte[]>(messageState, buffer));
				}
				catch (SocketException ex)
				{
					HandleCommunicationError(messageState.Handler, ex);
				}
				catch (ObjectDisposedException)
				{
					// If disposed, handle communication error was already done and we're just catching up on other threads. Supress it.
				}
			}
		}

		private void ProcessReceivedMessage(MessageState messageState)
		{
			int currentOffset = 0;
			int bytesRead = 0;

			while (_isDoingSomething)
			{
				// Check if we need a buffer
				if (messageState.Buffer == null)
				{
					// Get the next buffer
					BlockingQueue<KeyValuePair<byte[], int>> queue = null;
					_receiveBufferQueueLock.EnterReadLock();
					try
					{
						if (!_receiveBufferQueue.TryGetValue(messageState.Handler.GetHashCode(), out queue))
						{
							throw new Exception("FATAL: No receive queue created for current socket");
						}
					}
					finally
					{
						_receiveBufferQueueLock.ExitReadLock();
					}

					var receiveBufferEntry = queue.Dequeue();
					messageState.Buffer = receiveBufferEntry.Key;
					currentOffset = 0;
					bytesRead = receiveBufferEntry.Value;
				}

				// Check if we need to get our control byte values
				if (messageState.TotalBytesToRead == -1)
				{
					// We do, see if we have enough bytes received to get them
					if (currentOffset + _controlBytesPlaceholder.Length > currentOffset + bytesRead)
					{
						// We don't yet have enough bytes to read the control bytes, so get more bytes

						// Loop until we have enough data to proceed
						int bytesNeeded = _controlBytesPlaceholder.Length - bytesRead;
						while (bytesNeeded > 0)
						{
							// Combine the buffers
							BlockingQueue<KeyValuePair<byte[], int>> queue = null;
							_receiveBufferQueueLock.EnterReadLock();
							try
							{
								if (!_receiveBufferQueue.TryGetValue(messageState.Handler.GetHashCode(), out queue))
								{
									throw new Exception("FATAL: No receive queue created for current socket");
								}
							}
							finally
							{
								_receiveBufferQueueLock.ExitReadLock();
							}

							var nextBufferEntry = queue.Dequeue();
							var combinedBuffer = new byte[bytesRead + nextBufferEntry.Value];
							Buffer.BlockCopy(messageState.Buffer, currentOffset, combinedBuffer, 0, bytesRead);
							Buffer.BlockCopy(nextBufferEntry.Key, 0, combinedBuffer, bytesRead, nextBufferEntry.Value);
							// Set the new combined buffer and appropriate bytes read
							messageState.Buffer = combinedBuffer;
							// Reset bytes read and current offset
							currentOffset = 0;
							bytesRead = combinedBuffer.Length;
							// Subtract from bytes needed
							bytesNeeded -= nextBufferEntry.Value;
						}
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
				int originalTotalBytesToRead = messageState.TotalBytesToRead;
				messageState.TotalBytesToRead -= numberOfBytesToRead;

				// Check if we're done
				if (messageState.TotalBytesToRead == 0)
				{
					// Done, add to complete received messages
					CompleteMessage(messageState.Handler, messageState.ThreadId, messageState.Data.ToArray());
				}

				// Check if we have an overlapping message frame in our message AKA if the bytesRead was larger than the total bytes to read
				if (bytesRead > originalTotalBytesToRead)
				{
					// Get the number of bytes remaining to be read
					int bytesRemaining = bytesRead - numberOfBytesToRead;

					// Set total bytes to read to default
					messageState.TotalBytesToRead = -1;
					// Dispose and reinitialize data stream
					messageState.Data.Dispose();
					messageState.Data = new MemoryStream();

					// Now we have the next message, so recursively process it
					currentOffset += numberOfBytesToRead;
					bytesRead = bytesRemaining;
					continue;
				}

				// Only create a new message state if we are done with this message
				if (!(bytesRead < originalTotalBytesToRead))
				{
					// Get new state for the next message but transfer over handler
					Socket handler = messageState.Handler;
					messageState.Data.Dispose();
					messageState.Data = new MemoryStream();
					messageState.Handler = handler;
					messageState.TotalBytesToRead = -1;
					messageState.ThreadId = -1;
				}

				// Reset buffer for next message
				_bufferPool.Push(messageState.Buffer);
				messageState.Buffer = null;
			}
		}

		/// <summary>
		/// Handles an error in socket communication.
		/// </summary>
		/// <param name="socket">The socket that raised the exception.</param>
		/// <param name="ex">The exception that the socket raised.</param>
		private void HandleCommunicationError(Socket socket, Exception ex)
		{
			lock (socket)
			{
				// Close the socket
				try
				{
					socket.Shutdown(SocketShutdown.Both);
				}
				catch (SocketException)
				{
					// Socket was not able to be shutdown, likely because it was never opened
				}
				catch (ObjectDisposedException)
				{
					// Socket was already closed/disposed, so return out to prevent raising the Error event multiple times
					// This is most likely to happen when an error occurs during heavily multithreaded use
					return;
				}

				// Close / dispose the socket
				socket.Close();
			}

			// Release all multiplexer clients by signalling them
			_clientMultiplexerLock.EnterReadLock();
			try
			{
				foreach (var multiplexerData in _clientMultiplexer.Values)
				{
					multiplexerData.ManualResetEvent.Set();
				}
			}
			finally
			{
				_clientMultiplexerLock.ExitReadLock();
			}

			// Decrement the counter keeping track of the total number of clients connected to the server
			Interlocked.Decrement(ref _currentlyConnectedClients);

			// Release one from the max connections semaphore
			_maxConnectionsSemaphore.Release();

			// Raise the error event
			var error = Error;
			if (error != null)
			{
				var socketErrorArgs = new SocketErrorArgs(ex.Message);
				error(this, socketErrorArgs);
			}
		}

		private void CompleteMessage(Socket handler, int threadId, byte[] message)
		{
			// For server, notify the server to do something
			if (!_useClientMultiplexer)
			{
				var receivedMessage = _receiveMessagePool.Pop();
				receivedMessage.Socket = handler;
				receivedMessage.ThreadId = threadId;
				receivedMessage.Message = message;

				var messageReceived = MessageReceived;
				if (messageReceived != null)
				{
					var messageReceivedArgs = new MessageReceivedArgs(receivedMessage);
					messageReceived(this, messageReceivedArgs);
				}
				return;
			}

			// For client, set and signal multiplexer
			var multiplexerData = GetMultiplexerData(threadId);
			multiplexerData.Message = message;

			SignalMultiplexer(threadId);
		}

		private class MessageState
		{
			public byte[] Buffer = null;
			public Socket Handler = null;
			public MemoryStream Data = null;
			public int ThreadId = -1;
			public int TotalBytesToRead = -1;
		}

		/// <summary>
		/// A received message.
		/// </summary>
		public class ReceivedMessage
		{
			internal Socket Socket;
			internal int ThreadId;

			/// <summary>
			/// The message bytes.
			/// </summary>
			public byte[] Message;
		}

		private class MultiplexerData
		{
			public byte[] Message { get; set; }
			public ManualResetEvent ManualResetEvent { get; set; }
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

		private void ExtractControlBytes(byte[] buffer, int offset, out int messageLength, out int threadId)
		{
			messageLength = ((buffer[offset + 3] << 24) | (buffer[offset + 2] << 16) | (buffer[offset + 1] << 8) | buffer[offset + 0]) - _controlBytesPlaceholder.Length;
			threadId = (buffer[offset + 7] << 24) | (buffer[offset + 6] << 16) | (buffer[offset + 5] << 8) | buffer[offset + 4];
		}

		private MultiplexerData GetMultiplexerData(int threadId)
		{
			MultiplexerData multiplexerData = null;
			_clientMultiplexerLock.EnterReadLock();
			try
			{
				// Get from multiplexer by thread ID
				if (!_clientMultiplexer.TryGetValue(threadId, out multiplexerData))
				{
					throw new Exception("FATAL: multiplexer was missing entry for Thread ID " + threadId);
				}

				return multiplexerData;
			}
			finally
			{
				_clientMultiplexerLock.ExitReadLock();
			}
		}

		private MultiplexerData EnrollMultiplexer(int threadId)
		{
			var multiplexerData = new MultiplexerData { ManualResetEvent = _manualResetEventPool.Pop() };

			_clientMultiplexerLock.EnterWriteLock();
			try
			{
				// Add manual reset event for current thread
				_clientMultiplexer.Add(threadId, multiplexerData);
			}
			catch
			{
				throw new Exception("FATAL: multiplexer tried to add duplicate entry for Thread ID " + threadId);
			}
			finally
			{
				_clientMultiplexerLock.ExitWriteLock();
			}

			return multiplexerData;
		}

		private void UnenrollMultiplexer(int threadId)
		{
			MultiplexerData multiplexerData = null;
			_clientMultiplexerLock.EnterUpgradeableReadLock();
			try
			{
				// Get from multiplexer by thread ID
				if (!_clientMultiplexer.TryGetValue(threadId, out multiplexerData))
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
			_manualResetEventPool.Push(multiplexerData.ManualResetEvent);
		}

		private void SignalMultiplexer(int threadId)
		{
			MultiplexerData multiplexerData = null; ;
			_clientMultiplexerLock.EnterReadLock();
			try
			{
				// Get from multiplexer by thread ID
				if (!_clientMultiplexer.TryGetValue(threadId, out multiplexerData))
				{
					throw new Exception("FATAL: multiplexer was missing entry for Thread ID " + threadId);
				}

				multiplexerData.ManualResetEvent.Set();
			}
			finally
			{
				_clientMultiplexerLock.ExitReadLock();
			}
		}
	}
}

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
    public sealed class SocketRocker
    {
        private readonly Socket _socket = null;
        private readonly int _messageBufferSize = 0;
        private readonly int _maximumConnections = 0;
        // The semaphore that enforces the maximum numbers of simultaneous connections
        private readonly Semaphore _maxConnectionsSemaphore;
        private readonly bool _useNagleAlgorithm = false;

        private readonly Queue<byte[]> _completeReceivedMessages = null;
        private int _currentlyConnectedClients = 0;

        // The pools
        private readonly Pool<MessageState> _messageStatePool = null;

        private static readonly byte[] _controlBytesPlaceholder = new byte[] { 0, 0, 0, 0 };

        public SocketRocker(Socket socket, int messageBufferSize, int maximumConnections, bool useNagleAlgorithm)
        {
            // Sanitize
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }
            if (messageBufferSize < 256)
            {
                throw new ArgumentException("must be >= 256", "messageBufferSize");
            }
            if (maximumConnections <= 0)
            {
                throw new ArgumentException("must be > 0", "maximumConnections");
            }

            _socket = socket;
            _messageBufferSize = messageBufferSize;
            _maximumConnections = maximumConnections;
            _maxConnectionsSemaphore = new Semaphore(maximumConnections, maximumConnections);
            _useNagleAlgorithm = useNagleAlgorithm;

            _completeReceivedMessages = new Queue<byte[]>(maximumConnections);

            // Create the message state pool
            _messageStatePool = new Pool<MessageState>(maximumConnections, () => new MessageState());
            // Populate the pool
            for (int i = 0; i < maximumConnections; i++)
            {
                _messageStatePool.Push(new MessageState { Buffer = new byte[messageBufferSize] });
            }
        }

        public int CurrentlyConnectedClients
        {
            get
            {
                return _currentlyConnectedClients;
            }
        }

        public void Connect(IPEndPoint ipEndPoint)
        {
            _socket.NoDelay = !_useNagleAlgorithm;
            _socket.BeginConnect(ipEndPoint, ConnectCallback, null);
        }

        private void ConnectCallback(IAsyncResult asyncResult)
        {
            _socket.EndConnect(asyncResult);
        }

        public void Listen(IPEndPoint localEndpoint)
        {
            _socket.Bind(localEndpoint);
            _socket.Listen(_maximumConnections);

            // Post accept on the listening socket
            _socket.BeginAccept(AcceptCallback, null);
        }

        public void Close()
        {
            // Close the socket
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            // Throws if client process has already closed 
            catch
            {
                // Ignore
            }

            _socket.Close();

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
            messageState.ThreadId = -1;
            messageState.TotalBytesToRead = -1;
            messageState.Handler = handler;

            // Post receive on the handler socket
            handler.BeginReceive(messageState.Buffer, 0, messageState.Buffer.Length, 0, ReceiveCallback, messageState);
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            MessageState messageState = (MessageState)asyncResult;
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
            // Check if we need to get our control byte values
            if (messageState.TotalBytesToRead == -1)
            {
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
                    ExtractControlBytes(controlBytesBuffer, 0, out messageState.TotalBytesToRead);
                    // We know this is all we've received, so receive more data
                    messageState.Handler.BeginReceive(messageState.Buffer, 0, messageState.Buffer.Length, 0, ReceiveCallback, messageState);
                    return;
                }

                // Parse out control bytes
                ExtractControlBytes(messageState.Buffer, currentOffset, out messageState.TotalBytesToRead);
                // Offset the index by the control bytes
                currentOffset += _controlBytesPlaceholder.Length;
                // Take control bytes off of bytes read
                bytesRead -= _controlBytesPlaceholder.Length;
            }

            int numberOfBytesToRead = Math.Min(bytesRead, messageState.TotalBytesToRead);
            messageState.Data.Write(messageState.Buffer, currentOffset, numberOfBytesToRead);

            // Set total bytes read
            int originalTotalBytesToRead = messageState.TotalBytesToRead;
            messageState.TotalBytesToRead -= bytesRead;

            // Check if we're done
            if (messageState.TotalBytesToRead == 0)
            {
                // Done, add to complete received messages
                CompleteMessage(messageState.Data.ToArray());
                // Reset the message state
                messageState.Data.Dispose();
                messageState.TotalBytesToRead = -1;
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
                CompleteMessage(messageState.Data.ToArray());
                // Reset the message state
                messageState.Data.Dispose();
                messageState.TotalBytesToRead = -1;

                // Get the number of bytes remaining to be read
                int bytesRemaining = bytesRead - originalTotalBytesToRead;

                // Reset the message state
                messageState.Data = new MemoryStream();
                // Now we have the next message, so recursively process it
                ProcessReceivedMessage(messageState, originalTotalBytesToRead, bytesRemaining);
                return;
            }
        }

        public MemoryStream CreateMessageStream()
        {
            var memoryStream = new MemoryStream();
            // Add default placeholder message header
            memoryStream.Write(_controlBytesPlaceholder);
            return memoryStream;
        }

        public void Send(MemoryStream messageStream)
        {
            // Sanitize
            if (messageStream == null)
            {
                throw new ArgumentNullException("messageStream");
            }

            var message = messageStream.ToArray();
            messageStream.Dispose();

            // Set the control bytes on the message
            SetControlBytes(message);

            // Do the send
            _socket.BeginSend(message, 0, message.Length, 0, SendCallback, null);
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            // Complete the send
            _socket.EndSend(asyncResult);

            // Notify the send completed event
            var sendCompleted = SendCompleted;
            if (sendCompleted != null)
            {
                sendCompleted(this, EventArgs.Empty);
            }
        }

        public byte[] ReceiveMessage()
        {
            while (true)
            {
                // Block until something to do without locking
                if (_completeReceivedMessages.Count == 0)
                {
                    Thread.Sleep(250);
                    continue;
                }

                lock (_completeReceivedMessages)
                {
                    // Double lock check
                    if (_completeReceivedMessages.Count != 0)
                    {
                        return _completeReceivedMessages.Dequeue();
                    }
                }

                Thread.Sleep(250);
            }
        }

        public event EventHandler SendCompleted;

        public event EventHandler ReceiveCompleted;

        private void CompleteMessage(byte[] message)
        {
            lock (_completeReceivedMessages)
            {
                _completeReceivedMessages.Enqueue(message);
            }

            // Notify the receive completed event
            var receiveCompleted = ReceiveCompleted;
            if (receiveCompleted != null)
            {
                receiveCompleted(this, EventArgs.Empty);
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

        private static void SetControlBytes(byte[] buffer)
        {
            var length = buffer.Length;
            // Set little endian message length
            buffer[0] = (byte)length;
            buffer[1] = (byte)((length >> 8) & 0xFF);
            buffer[2] = (byte)((length >> 16) & 0xFF);
            buffer[3] = (byte)((length >> 24) & 0xFF);
        }

        private static void ExtractControlBytes(byte[] buffer, int offset, out int messageLength)
        {
            messageLength = (buffer[offset + 3] << 24) | (buffer[offset + 2] << 16) | (buffer[offset + 1] << 8) | buffer[offset + 0];
        }
    }
}

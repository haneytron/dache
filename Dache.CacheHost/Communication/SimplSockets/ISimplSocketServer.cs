using System;
using System.Net;

namespace SimplSockets
{
    /// <summary>
    /// Represents a socket server.
    /// </summary>
    public interface ISimplSocketServer : IDisposable
    {
        /// <summary>
        /// Begin listening for incoming connections. Once this is called, you must call Close before calling Listen again.
        /// </summary>
        /// <param name="localEndpoint">The local endpoint to listen on.</param>
        void Listen(EndPoint localEndpoint);

        /// <summary>
        /// Sends a message back to the client.
        /// </summary>
        /// <param name="message">The reply message to send.</param>
        /// <param name="receivedMessage">The received message which is being replied to.</param>
        void Reply(byte[] message, SimplSocket.ReceivedMessage receivedMessage);

        /// <summary>
        /// Closes the connection. Once this is called, you can call Listen again to start a new server.
        /// </summary>
        void Close();

        /// <summary>
        /// An event that is fired whenever a message is received. Hook into this to process messages and potentially call Reply to send a response.
        /// </summary>
        event EventHandler<MessageReceivedArgs> MessageReceived;

        /// <summary>
        /// An event that is fired whenever a socket communication error occurs.
        /// </summary>
        event EventHandler<SocketErrorArgs> Error;

        /// <summary>
        /// Disposes the instance and frees unmanaged resources.
        /// </summary>
        void Dispose();
    }
}

using System;
using System.Net;

namespace SimplSockets
{
    /// <summary>
    /// Represents a socket client.
    /// </summary>
    public interface ISimplSocketClient : IDisposable
    {
        /// <summary>
        /// Connects to an endpoint. Once this is called, you must call Close before calling Connect or Listen again. The errorHandler method 
        /// will not be called if the connection fails. Instead this method will return false.
        /// </summary>
        /// <param name="endPoint">The endpoint.</param>
        /// <param name="errorHandler">The error handler.</param>
        /// <returns>true if connection is successful, false otherwise.</returns>
        bool Connect(EndPoint endPoint, EventHandler<SocketErrorArgs> errorHandler);

        /// <summary>
        /// Sends a message to the server without waiting for a response (one-way communication).
        /// </summary>
        /// <param name="message">The message to send.</param>
        void Send(byte[] message);

        /// <summary>
        /// Sends a message to the server and receives the response to that message.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>The response message.</returns>
        byte[] SendReceive(byte[] message);

        /// <summary>
        /// Closes the connection. Once this is called, you can call Connect again to start a new connection.
        /// </summary>
        void Close();
    }
}

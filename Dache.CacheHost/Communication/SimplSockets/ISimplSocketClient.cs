using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace SimplSockets
{
    public interface ISimplSocketClient : IDisposable
    {
        /// <summary>
        /// Connects to an endpoint. Once this is called, you must call Close before calling Connect again.
        /// </summary>
        /// <param name="endPoint">The endpoint.</param>
        void Connect(EndPoint endPoint);

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

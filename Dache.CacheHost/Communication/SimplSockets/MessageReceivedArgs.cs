using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimplSockets
{
    /// <summary>
    /// Message received args.
    /// </summary>
    public class MessageReceivedArgs : EventArgs
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="receivedMessage">The received message.</param>
        internal MessageReceivedArgs(SimplSocket.ReceivedMessage receivedMessage)
        {
            ReceivedMessage = receivedMessage;
        }

        /// <summary>
        /// The received message.
        /// </summary>
        public SimplSocket.ReceivedMessage ReceivedMessage { get; private set; }
    }
}

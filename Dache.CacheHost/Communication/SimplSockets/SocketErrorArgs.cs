using System;

namespace SimplSockets
{
    /// <summary>
    /// Socket error args.
    /// </summary>
    public class SocketErrorArgs : EventArgs
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        internal SocketErrorArgs(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// The error message.
        /// </summary>
        public string ErrorMessage { get; private set; }
    }
}

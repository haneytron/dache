using System;
using System.Runtime.Serialization;

namespace Dache.Client.Exceptions
{
    /// <summary>
    /// Thrown when no cache hosts are available.
    /// </summary>
    [Serializable]
    public class NoCacheHostsAvailableException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NoCacheHostsAvailableException"/> class.
        /// </summary>
        public NoCacheHostsAvailableException()
            : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoCacheHostsAvailableException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public NoCacheHostsAvailableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoCacheHostsAvailableException"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public NoCacheHostsAvailableException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NoCacheHostsAvailableException"/> class.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        public NoCacheHostsAvailableException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}

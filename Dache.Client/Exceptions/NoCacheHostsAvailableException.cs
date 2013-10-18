using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Dache.Client.Exceptions
{
    /// <summary>
    /// Thrown when no cache hosts are available.
    /// </summary>
    [Serializable]
    public class NoCacheHostsAvailableException : Exception
    {
        /// <summary>
        /// The constructor.
        /// </summary>
        public NoCacheHostsAvailableException()
            : base()
        {

        }

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        public NoCacheHostsAvailableException(string message)
            : base(message)
        {

        }

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public NoCacheHostsAvailableException(string message, Exception innerException)
            : base(message, innerException)
        {

        }

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        public NoCacheHostsAvailableException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }
    }
}

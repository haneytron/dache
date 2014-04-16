using System;
using System.Web.SessionState;

namespace Dache.Client.Plugins.SessionState
{
    /// <summary>
    /// Encapsulates session state information.
    /// </summary>
    [Serializable]
    internal class DacheSessionState
    {
        /// <summary>
        /// Gets or sets the session ID.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the application name.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the datetime at which the object was created.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Gets or sets the datetime at which the object expires.
        /// </summary>
        public DateTime Expires { get; set; }

        /// <summary>
        /// Gets or sets the datetime at which the object was locked.
        /// </summary>
        public DateTime LockDate { get; set; }

        /// <summary>
        /// Gets or sets the lock ID.
        /// </summary>
        public int LockId { get; set; }

        /// <summary>
        /// Gets or sets the timeout.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="DacheSessionState"/> is locked.
        /// </summary>
        /// <value>
        ///   <c>true</c> if locked; otherwise, <c>false</c>.
        /// </value>
        public bool Locked { get; set; }

        /// <summary>
        /// Gets or sets the session items.
        /// </summary>
        public byte[] SessionItems { get; set; }

        /// <summary>
        /// Gets or sets the flags.
        /// </summary>
        public SessionStateActions Flags { get; set; }
    }
}

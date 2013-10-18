using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// The session ID.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// The application name.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// The datetime at which the object was created.
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// The datetime at which the object expires.
        /// </summary>
        public DateTime Expires { get; set; }

        /// <summary>
        /// The datetime at which the object was locked.
        /// </summary>
        public DateTime LockDate { get; set; }

        /// <summary>
        /// The lock ID.
        /// </summary>
        public int LockId { get; set; }

        /// <summary>
        /// The timeout.
        /// </summary>
        public int Timeout { get; set; }

        /// <summary>
        /// Whether or not the session state is locked.
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// The session items.
        /// </summary>
        public byte[] SessionItems { get; set; }

        /// <summary>
        /// The flags.
        /// </summary>
        public SessionStateActions Flags { get; set; }
    }
}

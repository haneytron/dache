using ProtoBuf;
using System;
using System.Web.SessionState;

namespace Dache.Client.Plugins.SessionState
{
    /// <summary>
    /// Encapsulates session state information.
    /// </summary>
    [ProtoContract]
    internal class DacheSessionState
    {
        /// <summary>
        /// The session ID.
        /// </summary>
        [ProtoMember(1)]
        public string SessionId { get; set; }

        /// <summary>
        /// The application name.
        /// </summary>
        [ProtoMember(2)]
        public string ApplicationName { get; set; }

        /// <summary>
        /// The datetime at which the object was created.
        /// </summary>
        [ProtoMember(3)]
        public DateTime Created { get; set; }

        /// <summary>
        /// The datetime at which the object expires.
        /// </summary>
        [ProtoMember(4)]
        public DateTime Expires { get; set; }

        /// <summary>
        /// The datetime at which the object was locked.
        /// </summary>
        [ProtoMember(5)]
        public DateTime LockDate { get; set; }

        /// <summary>
        /// The lock ID.
        /// </summary>
        [ProtoMember(6)]
        public int LockId { get; set; }

        /// <summary>
        /// The timeout.
        /// </summary>
        [ProtoMember(7)]
        public int Timeout { get; set; }

        /// <summary>
        /// Whether or not the session state is locked.
        /// </summary>
        [ProtoMember(8)]
        public bool Locked { get; set; }

        /// <summary>
        /// The session items.
        /// </summary>
        [ProtoMember(9)]
        public byte[] SessionItems { get; set; }

        /// <summary>
        /// The flags.
        /// </summary>
        [ProtoMember(10)]
        public SessionStateActions Flags { get; set; }
    }
}

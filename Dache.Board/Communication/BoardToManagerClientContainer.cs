using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Dache.Board.Communication
{
    /// <summary>
    /// Contains an instance of the board to manager client.
    /// </summary>
    internal static class BoardToManagerClientContainer
    {
        /// <summary>
        /// The instance.
        /// </summary>
        public static BoardToManagerClient Instance { get; set; }
    }
}
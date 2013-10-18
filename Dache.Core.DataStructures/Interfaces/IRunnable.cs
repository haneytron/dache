using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.Core.DataStructures.Interfaces
{
    /// <summary>
    /// Represents a runnable object.
    /// </summary>
    public interface IRunnable
    {
        /// <summary>
        /// Starts running.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops running.
        /// </summary>
        void Stop();
    }
}

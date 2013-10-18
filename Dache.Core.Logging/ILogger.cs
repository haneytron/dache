using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.Core.Logging
{
    /// <summary>
    /// Represents a logger. You can implement this interface to inject your own custom logging into a Dache client. Your implementation should be thread safe.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an information level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        void Info(string title, string message);

        /// <summary>
        /// Logs a warning level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        void Warn(string title, string message);

        /// <summary>
        /// Logs a warning level exception.
        /// </summary>
        /// <param name="prependedMessage">The message to prepend to the exception.</param>
        /// <param name="ex">The exception.</param>
        void Warn(string prependedMessage, Exception ex);

        /// <summary>
        /// Logs a warning level exception.
        /// </summary>
        /// <param name="ex">The exception.</param>
        void Warn(Exception ex);

        /// <summary>
        /// Logs an error level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        void Error(string title, string message);

        /// <summary>
        /// Logs an error level exception.
        /// </summary>
        /// <param name="prependedMessage">The message to prepend to the exception.</param>
        /// <param name="ex">The exception.</param>
        void Error(string prependedMessage, Exception ex);

        /// <summary>
        /// Logs an error level exception.
        /// </summary>
        /// <param name="ex">The exception.</param>
        void Error(Exception ex);
    }
}

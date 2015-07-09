using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Dache.Core.Logging
{
    /// <summary>
    /// Logs messages and exceptions to the debug window. Thread safe.
    /// </summary>
    public sealed class DebugLogger : ILogger
    {
        /// <summary>
        /// Logs an information level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        public void Info(string title, string message)
        {
            WriteLine("INFO", title + " : " + message);
        }

        /// <summary>
        /// Logs a warning level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        public void Warn(string title, string message)
        {
            WriteLine("WARN", title + " : " + message);
        }

        /// <summary>
        /// Logs a warning level exception.
        /// </summary>
        /// <param name="prependedMessage">The message to prepend to the exception.</param>
        /// <param name="ex">The exception.</param>
        public void Warn(string prependedMessage, Exception ex)
        {
            WriteLine("WARN", prependedMessage + " : " + ex);
        }

        /// <summary>
        /// Logs a warning level exception.
        /// </summary>
        /// <param name="ex">The exception.</param>
        public void Warn(Exception ex)
        {
            WriteLine("WARN", ex.ToString());
        }

        /// <summary>
        /// Logs an error level message.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="message">The message.</param>
        public void Error(string title, string message)
        {
            WriteLine("ERROR", title + " : " + message);
        }

        /// <summary>
        /// Logs an error level exception.
        /// </summary>
        /// <param name="prependedMessage">The message to prepend to the exception.</param>
        /// <param name="ex">The exception.</param>
        public void Error(string prependedMessage, Exception ex)
        {
            WriteLine("ERROR", prependedMessage + " : " + ex);
        }

        /// <summary>
        /// Logs an error level exception.
        /// </summary>
        /// <param name="ex">The exception.</param>
        public void Error(Exception ex)
        {
            WriteLine("ERROR", ex.ToString());
        }

        /// <summary>
        /// Writes a line to the log file.
        /// </summary>
        /// <param name="level">The logging level.</param>
        /// <param name="message">The message.</param>
        private void WriteLine(string level, string message)
        {
            var text = string.Format("{0} {1} {2}", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"), level, message);
            Debug.WriteLine(text);
        }
    }
}

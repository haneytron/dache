namespace Dache.CacheHost
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

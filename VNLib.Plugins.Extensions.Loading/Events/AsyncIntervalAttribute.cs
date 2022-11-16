using System;

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// When added to a method schedules it as a callback on a specified interval when 
    /// the plugin is loaded, and stops when unloaded
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AsyncIntervalAttribute : Attribute
    {
        internal readonly TimeSpan Interval;

        /// <summary>
        /// Intializes the <see cref="AsyncIntervalAttribute"/> with the specified timeout in milliseconds
        /// </summary>
        /// <param name="milliseconds">The interval in milliseconds</param>
        public AsyncIntervalAttribute(int milliseconds)
        {
            Interval = TimeSpan.FromMilliseconds(milliseconds);
        }
    }
}

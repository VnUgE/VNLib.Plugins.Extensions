using System;

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// When added to a method schedules it as a callback on a specified interval when 
    /// the plugin is loaded, and stops when unloaded
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class ConfigurableAsyncIntervalAttribute : Attribute
    {
        internal readonly string IntervalPropertyName;
        internal readonly IntervalResultionType Resolution;

        /// <summary>
        /// Initializes a <see cref="ConfigurableAsyncIntervalAttribute"/> with the specified
        /// interval property name
        /// </summary>
        /// <param name="configPropName">The configuration property name for the event interval</param>
        /// <param name="resolution">The time resoltion for the event interval</param>
        public ConfigurableAsyncIntervalAttribute(string configPropName, IntervalResultionType resolution)
        {
            IntervalPropertyName = configPropName;
            Resolution = resolution;
        }
    }
}

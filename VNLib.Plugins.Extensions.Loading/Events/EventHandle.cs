using System;
using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils;
using VNLib.Utils.Extensions;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// Represents a handle to a scheduled event interval that is managed by the plugin but may be cancled by disposing the instance
    /// </summary>
    public class EventHandle : VnDisposeable
    {
        private readonly PluginBase _pbase;
        private readonly Timer _eventTimer;
        private readonly TimeSpan _interval;
        private readonly AsyncSchedulableCallback _callback;

        internal EventHandle(AsyncSchedulableCallback callback, TimeSpan interval, PluginBase pbase)
        {
            _pbase = pbase;
            _interval = interval;
            _callback = callback;

            //Init new timer
            _eventTimer = new(OnTimerElapsed, this, interval, interval);

            //Register dispose to unload token
            _ = pbase.UnloadToken.RegisterUnobserved(Dispose);
        }

        private void OnTimerElapsed(object? state)
        {
            //Run on task scheuler
            _ = Task.Run(RunInterval)
                .ConfigureAwait(false);
        }
       
        private async Task RunInterval()
        {
            try
            {
                await _callback(_pbase.Log, _pbase.UnloadToken);
            }
            catch (OperationCanceledException)
            {
                //unloaded
                _pbase.Log.Verbose("Interval callback canceled due to plugin unload or other event cancellation");
            }
            catch (Exception ex)
            {
                _pbase.Log.Error(ex, "Unhandled exception raised during timer callback");
            }
        }

        /// <summary>
        /// Invokes the event handler manually and observes the result.
        /// This method writes execptions to the plugin's default log provider.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public Task ManualInvoke()
        {
            Check();
            return Task.Run(RunInterval);
        }


        /// <summary>
        /// Pauses the event timer until the <see cref="OpenHandle"/> is released or disposed
        /// then resumes to the inital interval period
        /// </summary>
        /// <returns>A <see cref="OpenHandle"/> that restores the timer to its initial state when disposed</returns>
        /// <exception cref="ObjectDisposedException"></exception>
        public OpenHandle Pause()
        {
            Check();
            return _eventTimer.Stop(_interval);
        }

        ///<inheritdoc/>
        protected override void Free()
        {
            _eventTimer.Dispose();
        }
    }
}

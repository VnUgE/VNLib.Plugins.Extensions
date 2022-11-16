using System.Threading;
using System.Threading.Tasks;

using VNLib.Utils.Logging;

namespace VNLib.Plugins.Extensions.Loading.Events
{
    /// <summary>
    /// Exposes a type for asynchronous event schelueling
    /// </summary>
    public interface IIntervalScheduleable
    {
        /// <summary>
        /// A method that is called when the interval time has elapsed
        /// </summary>
        /// <param name="log">The plugin default log provider</param>
        /// <param name="cancellationToken">A token that may cancel an operations if the plugin becomes unloaded</param>
        /// <returns>A task that resolves when the async operation completes</returns>
        Task OnIntervalAsync(ILogProvider log, CancellationToken cancellationToken);
    }
}

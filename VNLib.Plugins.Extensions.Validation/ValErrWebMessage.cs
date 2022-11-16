using System.Collections;
using System.Text.Json.Serialization;

namespace VNLib.Plugins.Extensions.Validation
{
    /// <summary>
    /// Extends the <see cref="WebMessage"/> class with provisions for a collection of validations
    /// </summary>
    public class ValErrWebMessage:WebMessage
    {
        /// <summary>
        /// A collection of error messages to send to clients
        /// </summary>
        [JsonPropertyName("errors")]
        public ICollection Errors { get; set; }
    }
}

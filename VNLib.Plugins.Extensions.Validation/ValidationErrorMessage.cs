using System.Text.Json.Serialization;

namespace VNLib.Plugins.Extensions.Validation
{
    public class ValidationErrorMessage
    {
        [JsonPropertyName("property")]
        public string PropertyName { get; set; }
        [JsonPropertyName("message")]
        public string ErrorMessage { get; set; }
    }
}

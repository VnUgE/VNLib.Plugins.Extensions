#nullable enable

namespace VNLib.Plugins.Extensions.Loading.Configuration
{
    public sealed class S3Config
    {
        public string? ServerAddress { get; init; }
        public string? ClientId { get; init; }        
        public string? ClientSecret { get; init; }
        public string? BaseBucket { get; init; }
        public bool? UseSsl { get; init; }
        public string? Region { get; init; }
    }
}

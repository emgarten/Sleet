using NuGet.Versioning;

namespace Sleet
{
    public class FeedCapability
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public override string ToString()
        {
            return $"{Name}:{Version.ToNormalizedString()}".ToLowerInvariant();
        }

        public static FeedCapability Parse(string s)
        {
            var parts = s.ToLowerInvariant().Split(':');
            return new FeedCapability
            {
                Name = parts[0],
                Version = parts.Length > 0 ? SemanticVersion.Parse(parts[1]) : new SemanticVersion(1, 0, 0)
            };
        }
    }
}

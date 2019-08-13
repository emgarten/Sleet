using System.Collections.Generic;
using NuGet.Versioning;

namespace Sleet
{
    public class FeedRequirements
    {
        /// <summary>
        /// Original version of sleet used to create the feed. This is deprecated as a means for
        /// checking if an upgrade is needed.
        /// </summary>
        public SemanticVersion CreatorSleetVersion { get; set; } = new SemanticVersion(1, 0, 0);

        /// <summary>
        /// Required version sleet to work with the feed. The simpliest way to exclude older clients from the feed.
        /// </summary>
        public VersionRange RequiredVersion { get; set; } = VersionRange.All;

        /// <summary>
        /// Required capabilities. This offers fined grained control over when an upgrade is required. If a new
        /// feature is added to Sleet but not used in the feed we can avoid upgrading by looking for capabilities
        /// that are not supported.
        /// </summary>
        public List<FeedCapability> RequiredCapabilities { get; set; } = new List<FeedCapability>();
    }
}

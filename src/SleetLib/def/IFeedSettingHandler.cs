using System.Threading.Tasks;

namespace Sleet
{
    /// <summary>
    /// Apply additional operations when a feed setting is applied.
    /// The setting is managed in sleet.settings.json outside of this
    /// handler. Handlers are only used for advanced settings.
    /// </summary>
    public interface IFeedSettingHandler
    {
        /// <summary>
        /// Name in sleet.settings.json
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Called when the setting it set by the user.
        /// </summary>
        Task Set(string value);

        /// <summary>
        /// Called when the setting is cleared by the user.
        /// </summary>
        Task UnSet();
    }
}

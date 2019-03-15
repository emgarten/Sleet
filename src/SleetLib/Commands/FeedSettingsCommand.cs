using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Sleet
{
    /// <summary>
    /// --unset
    /// --unset-all
    /// --get
    /// --get-all
    /// --set
    /// </summary>
    public static class FeedSettingsCommand
    {
        public static async Task<bool> RunAsync(
            LocalSettings settings,
            ISleetFileSystem source,
            bool unsetAll,
            bool getAll,
            IEnumerable<string> getSettings,
            IEnumerable<string> unsetSettings,
            IEnumerable<string> setSettings,
            ILogger log,
            CancellationToken token)
        {
            log.LogMinimal($"Reading feed {source.BaseURI.AbsoluteUri}");

            // Check if already initialized
            using (var feedLock = await SourceUtility.VerifyInitAndLock(settings, source, "Feed settings", log, token))
            {
                // Validate source
                var success = await UpgradeUtility.EnsureFeedVersionMatchesTool(source, log, token);

                success &= await ApplySettingsAsync(source, unsetAll, getAll, getSettings, unsetSettings, setSettings, log, token);

                log.LogMinimal($"Run 'recreate' to rebuild the feed with the new settings.");

                return success;
            }
        }

        public static async Task<bool> ApplySettingsAsync(
            ISleetFileSystem source,
            bool unsetAll,
            bool getAll,
            IEnumerable<string> getSettings,
            IEnumerable<string> unsetSettings,
            IEnumerable<string> setSettings,
            ILogger log,
            CancellationToken token)
        {
            var feedSettings = FeedSettingsUtility.GetSettingsFileFromFeed(source);
            var feedSettingsJson = await feedSettings.GetJson(log, CancellationToken.None);

            var settings = FeedSettingsUtility.GetSettings(feedSettingsJson);

            var getKeys = new SortedSet<string>(getSettings, StringComparer.OrdinalIgnoreCase);
            var setKeys = new SortedSet<string>(setSettings, StringComparer.OrdinalIgnoreCase);
            var unsetKeys = new SortedSet<string>(unsetSettings, StringComparer.OrdinalIgnoreCase);

            if ((getKeys.Count + setKeys.Count + unsetKeys.Count) == 0 && !unsetAll && !getAll)
            {
                throw new ArgumentException("No arguments specified.");
            }

            if (getAll)
            {
                getKeys.UnionWith(settings.Keys);
            }

            if (unsetAll)
            {
                unsetKeys.UnionWith(settings.Keys);
            }

            if (getKeys.Count > 0 && (setKeys.Count + unsetKeys.Count) > 0)
            {
                throw new ArgumentException("Invalid combination of arguments. Get may not be combined with set or unset.");
            }

            // Get
            foreach (var key in getKeys)
            {
                if (!settings.TryGetValue(key, out var value))
                {
                    value = "not found!";
                }

                log.LogMinimal($"{key} : {value}");
            }

            // Unset
            foreach (var key in unsetKeys)
            {
                if (settings.ContainsKey(key))
                {
                    settings.Remove(key);
                }
            }

            // Set
            var setKeysSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var input in setKeys)
            {
                var parts = input.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2 || string.IsNullOrEmpty(parts[0]?.Trim()) || string.IsNullOrEmpty(parts[1]?.Trim()))
                {
                    throw new ArgumentException("Value must be in the form {key}:{value}. Invalid: '" + input + "'");
                }

                var key = parts[0].Trim().ToLowerInvariant();
                var value = parts[1].Trim();

                if (!setKeysSeen.Add(key))
                {
                    throw new ArgumentException($"Duplicate values for '{key}'. This value may only be set once.");
                }

                if (settings.ContainsKey(key))
                {
                    settings[key] = value;
                }
                else
                {
                    settings.Add(key, value);
                }
            }

            if (setKeys.Count > 0 || unsetKeys.Count > 0)
            {
                FeedSettingsUtility.Set(feedSettingsJson, settings);

                log.LogMinimal($"Updating settings");

                await feedSettings.Write(feedSettingsJson, log, token);

                // Save all
                log.LogMinimal($"Committing changes to {source.BaseURI.AbsoluteUri}");

                return await source.Commit(log, token);
            }

            return true;
        }
    }
}
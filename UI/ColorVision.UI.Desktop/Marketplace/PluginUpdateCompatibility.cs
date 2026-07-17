using ColorVision.UI.Marketplace;
using System.Reflection;

namespace ColorVision.UI.Desktop.Marketplace
{
    public static class PluginUpdateCompatibility
    {
        public static Version? GetCurrentHostVersion() => Assembly.GetEntryAssembly()?.GetName().Version;

        public static MarketplacePluginVersionInfo? SelectLatestCompatibleVersion(
            Version? installedVersion,
            MarketplacePluginDetail? detail,
            Version hostVersion)
        {
            if (detail == null)
                return null;

            List<MarketplacePluginVersionInfo> versions = detail.Versions
                .Concat(detail.ArchivedVersions)
                .OrderByDescending(version => ParseVersion(version.Version) ?? new Version())
                .ThenByDescending(version => version.CreatedAt)
                .ToList();

            foreach (MarketplacePluginVersionInfo versionInfo in versions)
            {
                Version? candidateVersion = ParseVersion(versionInfo.Version);
                if (candidateVersion == null || (installedVersion != null && candidateVersion <= installedVersion))
                    continue;

                string? requiresVersion = versionInfo.RequiresVersion ?? detail.RequiresVersion;
                if (!IsCompatibleWithHostVersion(requiresVersion, hostVersion))
                    continue;

                return CopyWithEffectiveHostRequirement(versionInfo, requiresVersion);
            }

            bool latestVersionHasDetailedMetadata = versions.Any(version =>
                string.Equals(version.Version, detail.LatestVersion, StringComparison.OrdinalIgnoreCase));
            Version? fallbackVersion = ParseVersion(detail.LatestVersion);
            if (latestVersionHasDetailedMetadata ||
                fallbackVersion == null ||
                (installedVersion != null && fallbackVersion <= installedVersion) ||
                !IsCompatibleWithHostVersion(detail.RequiresVersion, hostVersion))
            {
                return null;
            }

            return new MarketplacePluginVersionInfo
            {
                Version = detail.LatestVersion!,
                RequiresVersion = detail.RequiresVersion,
                ChangeLog = detail.Changelog,
            };
        }

        public static bool IsCompatibleWithHostVersion(string? requiresVersion, Version hostVersion)
        {
            if (string.IsNullOrWhiteSpace(requiresVersion))
                return true;

            return Version.TryParse(requiresVersion.Trim(), out Version? requiredVersion)
                && hostVersion >= requiredVersion;
        }

        private static MarketplacePluginVersionInfo CopyWithEffectiveHostRequirement(MarketplacePluginVersionInfo versionInfo, string? requiresVersion)
        {
            return new MarketplacePluginVersionInfo
            {
                Version = versionInfo.Version,
                RequiresVersion = requiresVersion,
                ChangeLog = versionInfo.ChangeLog,
                FileSize = versionInfo.FileSize,
                FileHash = versionInfo.FileHash,
                DownloadCount = versionInfo.DownloadCount,
                CreatedAt = versionInfo.CreatedAt,
                Source = versionInfo.Source,
            };
        }

        private static Version? ParseVersion(string? value) => Version.TryParse(value, out Version? version) ? version : null;
    }
}

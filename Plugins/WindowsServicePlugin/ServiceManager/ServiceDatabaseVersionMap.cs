using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// Maps a CVWindowsService release version to the business database used by that release.
    /// </summary>
    public static class ServiceDatabaseVersionMap
    {
        public const string LegacyDatabaseName = "color_vision";
        public const string Version4DatabaseName = "color_vision_4xx";

        public static Version Version4DatabaseStart { get; } = new Version(4, 0, 0, 0);

        private static readonly Regex PackageVersionRegex = new(
            @"CVWindowsService\[(?<version>\d+(?:\.\d+){1,3})\]",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static string GetDatabaseName(Version serviceVersion)
        {
            ArgumentNullException.ThrowIfNull(serviceVersion);
            return serviceVersion.Major >= Version4DatabaseStart.Major
                ? Version4DatabaseName
                : LegacyDatabaseName;
        }

        public static string ResolveDatabaseName(Version? serviceVersion, string? configuredDatabase)
        {
            if (serviceVersion != null)
            {
                return GetDatabaseName(serviceVersion);
            }

            return string.IsNullOrWhiteSpace(configuredDatabase)
                ? Version4DatabaseName
                : configuredDatabase.Trim();
        }

        public static bool TryParsePackageVersion(string? packagePath, out Version version)
        {
            version = new Version();
            string fileName = Path.GetFileName(packagePath) ?? string.Empty;
            Match match = PackageVersionRegex.Match(fileName);
            return match.Success && Version.TryParse(match.Groups["version"].Value, out version!);
        }
    }

    /// <summary>
    /// Resolves CVWindowsService versions from a selected package or an installed service tree.
    /// </summary>
    public static class ServicePackageVersionResolver
    {
        private static readonly string[] ServiceExecutableRelativePaths =
        [
            Path.Combine("RegWindowsService", "RegWindowsService.exe"),
            Path.Combine("CVMainWindowsService_x64", "CVMainWindowsService_x64.exe"),
            Path.Combine("CVMainWindowsService_dev", "CVMainWindowsService_dev.exe")
        ];

        public static bool TryGetPackageVersion(string packagePath, out Version version)
        {
            if (TryReadEmbeddedExecutableVersion(packagePath, out version))
            {
                return true;
            }

            return ServiceDatabaseVersionMap.TryParsePackageVersion(packagePath, out version);
        }

        public static bool TryGetInstalledVersion(string? baseLocation, out Version version)
        {
            version = new Version();
            if (string.IsNullOrWhiteSpace(baseLocation))
            {
                return false;
            }

            foreach (string root in EnumerateServiceRoots(baseLocation))
            {
                foreach (string relativePath in ServiceExecutableRelativePaths)
                {
                    if (TryGetFileVersion(Path.Combine(root, relativePath), out version))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryReadEmbeddedExecutableVersion(string packagePath, out Version version)
        {
            version = new Version();
            if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
            {
                return false;
            }

            string tempFile = Path.Combine(Path.GetTempPath(), $"cvws-version-{Guid.NewGuid():N}.exe");
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(packagePath);
                ZipArchiveEntry? entry = archive.Entries
                    .OrderBy(GetExecutableEntryOrder)
                    .FirstOrDefault(item => GetExecutableEntryOrder(item) < int.MaxValue);
                if (entry == null || entry.Length <= 0 || entry.Length > 256L * 1024 * 1024)
                {
                    return false;
                }

                using (Stream input = entry.Open())
                using (FileStream output = File.Create(tempFile))
                {
                    input.CopyTo(output);
                }

                return TryGetFileVersion(tempFile, out version);
            }
            catch
            {
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                }
            }
        }

        private static int GetExecutableEntryOrder(ZipArchiveEntry entry)
        {
            string normalized = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            for (int i = 0; i < ServiceExecutableRelativePaths.Length; i++)
            {
                if (normalized.EndsWith(ServiceExecutableRelativePaths[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        private static bool TryGetFileVersion(string filePath, out Version version)
        {
            version = new Version();
            if (!File.Exists(filePath))
            {
                return false;
            }

            try
            {
                string? fileVersion = FileVersionInfo.GetVersionInfo(filePath).FileVersion;
                return !string.IsNullOrWhiteSpace(fileVersion) && Version.TryParse(fileVersion, out version!);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> EnumerateServiceRoots(string baseLocation)
        {
            string fullBasePath;
            try
            {
                fullBasePath = Path.GetFullPath(baseLocation);
            }
            catch
            {
                yield break;
            }

            yield return fullBasePath;

            string nested = Path.Combine(fullBasePath, "CVWindowsService");
            if (!string.Equals(nested, fullBasePath, StringComparison.OrdinalIgnoreCase))
            {
                yield return nested;
            }
        }
    }
}

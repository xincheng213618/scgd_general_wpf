using ColorVision.UI.Plugins;
using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.UI.Desktop.Marketplace
{
    internal sealed class MarketplacePackagePreflight
    {
        public bool IsValid { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;
    }

    internal static class MarketplacePackagePreflightReader
    {
        private const long MaximumManifestBytes = 1_048_576;

        public static MarketplacePackagePreflight Read(string packagePath, MarketplacePackageRequest? request = null)
        {
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(packagePath);
                List<ZipArchiveEntry> allManifestEntries = archive.Entries
                    .Where(entry => string.Equals(Path.GetFileName(entry.FullName), "manifest.json", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (allManifestEntries.Any(entry => !IsSafeArchivePath(entry.FullName)))
                    return Invalid("The package contains an unsafe manifest path.");

                List<ZipArchiveEntry> manifestEntries = allManifestEntries
                    .Where(entry => GetPathSegments(entry.FullName).Length <= 2)
                    .ToList();
                if (manifestEntries.Count == 0)
                    return Valid();
                if (manifestEntries.Count > 1)
                    return Invalid("The package contains more than one top-level manifest.json.");

                ZipArchiveEntry manifestEntry = manifestEntries[0];
                if (manifestEntry.Length > MaximumManifestBytes)
                    return Invalid($"manifest.json exceeds the {MaximumManifestBytes:N0}-byte inspection limit.");

                PluginManifest manifest = ReadManifest(manifestEntry);
                if (request != null
                    && !string.IsNullOrWhiteSpace(manifest.Id)
                    && !string.Equals(manifest.Id.Trim(), request.PluginId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return Invalid($"Manifest id '{manifest.Id.Trim()}' does not match requested package '{request.PluginId.Trim()}'.");
                }

                return Valid();
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                return Invalid(ex.Message);
            }
        }

        private static PluginManifest ReadManifest(ZipArchiveEntry entry)
        {
            using Stream stream = entry.Open();
            using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
            var jsonBuilder = new StringBuilder((int)Math.Min(entry.Length, MaximumManifestBytes));
            char[] buffer = new char[8_192];
            int charactersRead;
            while ((charactersRead = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (jsonBuilder.Length + charactersRead > MaximumManifestBytes)
                    throw new InvalidDataException($"manifest.json exceeds the {MaximumManifestBytes:N0}-character inspection limit.");
                jsonBuilder.Append(buffer, 0, charactersRead);
            }

            string json = jsonBuilder.ToString();
            return JsonConvert.DeserializeObject<PluginManifest>(json, new JsonSerializerSettings { MaxDepth = 64 })
                ?? throw new JsonException("manifest.json must contain a JSON object.");
        }

        private static bool IsSafeArchivePath(string path)
        {
            string[] segments = GetPathSegments(path);
            return segments.Length > 0
                && !Path.IsPathRooted(path)
                && !path.Contains(':')
                && segments.All(segment => segment is not "." and not "..");
        }

        private static string[] GetPathSegments(string path)
        {
            return path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        }

        private static MarketplacePackagePreflight Valid()
        {
            return new MarketplacePackagePreflight { IsValid = true };
        }

        private static MarketplacePackagePreflight Invalid(string message)
        {
            return new MarketplacePackagePreflight
            {
                ErrorMessage = string.IsNullOrWhiteSpace(message) ? "Unknown package inspection error." : message.Trim(),
            };
        }
    }
}

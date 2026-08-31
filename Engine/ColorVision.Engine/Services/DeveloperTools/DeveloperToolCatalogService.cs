using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.DeveloperTools
{
    public sealed class DeveloperToolCatalogService
    {
        private static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 4 * 1024 * 1024 };

        public async Task<IReadOnlyList<DeveloperToolRelease>> GetReleasesAsync(DeveloperToolKind kind, CancellationToken cancellationToken)
        {
            string content = await Client.GetStringAsync(kind == DeveloperToolKind.Python
                ? "https://www.python.org/downloads/windows/"
                : "https://nodejs.org/dist/index.json", cancellationToken).ConfigureAwait(false);
            return kind == DeveloperToolKind.Python ? ParsePythonReleases(content) : ParseNodeReleases(content);
        }

        public static IReadOnlyList<DeveloperToolRelease> ParsePythonReleases(string html) => Regex.Matches(html,
                @"https://www\.python\.org/ftp/python/(?<version>3\.\d+\.\d+)/python-\k<version>-amd64\.exe",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Select(match => Version.Parse(match.Groups["version"].Value)).Distinct()
            .Where(version => version.Minor >= 12).OrderDescending()
            .GroupBy(version => version.Minor).Take(3)
            .Select(group => new DeveloperToolRelease(DeveloperToolKind.Python, group.First())).ToArray();

        public static IReadOnlyList<DeveloperToolRelease> ParseNodeReleases(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            var versions = new List<DeveloperToolRelease>();
            foreach (JsonElement entry in document.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("lts", out var lts) || lts.ValueKind != JsonValueKind.String
                    || !entry.TryGetProperty("version", out var versionText)
                    || !Version.TryParse(versionText.GetString()?.TrimStart('v'), out Version? version)
                    || version.Major < 22 || version.Build < 0 || version.Revision >= 0
                    || !entry.TryGetProperty("files", out var files)
                    || !files.EnumerateArray().Any(file => file.GetString() == "win-x64-msi")) continue;
                string npm = entry.TryGetProperty("npm", out var packageVersion) ? packageVersion.GetString() ?? "" : "";
                versions.Add(new DeveloperToolRelease(DeveloperToolKind.NodeJs, version, npm));
            }
            return versions.OrderByDescending(item => item.Version).GroupBy(item => item.Version.Major)
                .Take(2).Select(group => group.First()).ToArray();
        }

        public async Task<string> GetOfficialSha256Async(DeveloperToolRelease release, CancellationToken cancellationToken)
        {
            // The mirror never supplies the expected hash. The official HTTPS origin is the trust anchor here.
            if (release.Kind == DeveloperToolKind.Python)
            {
                string json = await Client.GetStringAsync(release.OfficialUri.AbsoluteUri + ".sigstore", cancellationToken).ConfigureAwait(false);
                return ParsePythonSha256(json);
            }
            string checksums = await Client.GetStringAsync(new Uri(release.OfficialUri, "SHASUMS256.txt"), cancellationToken).ConfigureAwait(false);
            return ParseNodeSha256(checksums, release.FileName);
        }

        public static string ParsePythonSha256(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("messageSignature", out var signature)
                && signature.TryGetProperty("messageDigest", out var digest)
                && digest.GetProperty("algorithm").GetString() == "SHA2_256")
            {
                byte[] bytes = Convert.FromBase64String(digest.GetProperty("digest").GetString() ?? "");
                if (bytes.Length == 32) return Convert.ToHexString(bytes);
            }
            throw new InvalidDataException("官网未提供可识别的 SHA256 校验信息，已阻止安装。请使用官方安装页。");
        }

        public static string ParseNodeSha256(string checksums, string fileName)
        {
            foreach (string line in checksums.Split('\n'))
            {
                string[] parts = line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[1] == fileName && Regex.IsMatch(parts[0], @"\A[0-9a-fA-F]{64}\z", RegexOptions.CultureInvariant))
                    return parts[0].ToUpperInvariant();
            }
            throw new InvalidDataException("官网校验清单中没有所选安装包，已阻止安装。");
        }
    }
}

using ColorVision.Engine.Services.PhyCameras.Licenses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.PhySpectrums
{
    internal sealed class SpectrumLicenseUpdateService
    {
        private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(45) };
        private readonly HttpClient client;
        internal SpectrumLicenseUpdateService(HttpClient? client = null) => this.client = client ?? Client;

        internal async Task<LicenseModel> DownloadAsync(string sn, CancellationToken cancellationToken = default)
        {
            sn = PhySpectrumStore.NormalizeSerial(sn);
            using var response = await client.PostAsJsonAsync("https://color-vision.picp.net/license/api/v1/license/onlyDownloadLicense", new { macSn = sn }, cancellationToken).ConfigureAwait(false);
            string mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!response.IsSuccessStatusCode || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase) || mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new InvalidDataException($"{sn} · HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {body[..Math.Min(body.Length, 1000)]}");
            }
            // Read the archive in memory; never trust a server-provided filename or extract archive paths.
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes, false);
            return ReadArchive(stream, sn);
        }

        internal static LicenseModel ReadFile(string filePath, string sn) =>
            DeviceLicenseImport.ReadFile(filePath, DeviceLicenseImport.Spectrum(sn));

        internal static IReadOnlyList<LicenseModel> ReadFiles(IEnumerable<string> filePaths, IEnumerable<string> knownSerials, IReadOnlyList<LicenseModel> existing) =>
            DeviceLicenseImport.ReadFiles(filePaths, knownSerials.Select(DeviceLicenseImport.Spectrum).ToArray(), existing, requiredType: 1);

        internal static LicenseModel ReadArchive(Stream stream, string sn) =>
            DeviceLicenseImport.ReadArchive(stream, DeviceLicenseImport.Spectrum(sn));

        internal static LicenseModel ParseLicense(string value, string sn) =>
            DeviceLicenseImport.Parse(value, DeviceLicenseImport.Spectrum(sn));
    }
}

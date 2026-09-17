using ColorVision.Engine.Services.PhyCameras.Licenses;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
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

        internal static LicenseModel ReadFile(string filePath, string sn)
        {
            sn = PhySpectrumStore.NormalizeSerial(sn);
            if (string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = File.OpenRead(filePath);
                return ReadArchive(stream, sn);
            }
            if (!string.Equals(Path.GetExtension(filePath), ".lic", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(Properties.Resources.UnsupportedLicenseFileExtension);
            if (!string.Equals(Path.GetFileNameWithoutExtension(filePath), sn, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(string.Format(Properties.Resources.SpectrumLicenseSerialMismatch, sn));
            return ParseLicense(File.ReadAllText(filePath), sn);
        }

        internal static LicenseModel ReadArchive(Stream stream, string sn)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
            var matches = archive.Entries.Where(e => string.Equals(Path.GetExtension(e.Name), ".lic", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileNameWithoutExtension(e.Name), sn, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException(string.Format(Properties.Resources.SpectrumLicenseSerialMismatch, sn));
            using var reader = new StreamReader(matches[0].Open(), Encoding.UTF8);
            return ParseLicense(reader.ReadToEnd(), sn);
        }

        internal static LicenseModel ParseLicense(string value, string sn)
        {
            try
            {
                // The legacy payload has no SN field; the <SN>.lic name is its identity contract.
                // Signature validation remains the native driver's responsibility when it loads the license.
                var payload = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(value)));
                string? model = payload.Value<string>("device_mode");
                if (string.IsNullOrWhiteSpace(model) || !long.TryParse(payload.Value<string>("expiry_date"), NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiry))
                    throw new FormatException(Properties.Resources.SpectrumInvalidLicense);
                return new LicenseModel
                {
                    MacAddress = sn, LiceType = 1, LicenseValue = value,
                    Model = model, CusTomerName = payload.Value<string>("licensee"),
                    ExpiryDate = DateTimeOffset.FromUnixTimeSeconds(expiry).LocalDateTime
                };
            }
            catch (Exception ex) when (ex is FormatException or Newtonsoft.Json.JsonException or ArgumentException)
            {
                throw new InvalidDataException(Properties.Resources.SpectrumInvalidLicense, ex);
            }
        }
    }
}

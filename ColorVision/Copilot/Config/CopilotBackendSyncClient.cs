using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotBackendConfigResponse
    {
        public int SchemaVersion { get; set; }

        public string Revision { get; set; } = string.Empty;

        public DateTimeOffset GeneratedAt { get; set; }

        public string? DefaultProfileId { get; set; }

        public List<CopilotBackendProfile> Profiles { get; set; } = new();
    }

    internal sealed class CopilotBackendProfile
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string VendorType { get; set; } = string.Empty;

        public string ProviderType { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        public bool AllowInsecureHttp { get; set; }

        public string ReasoningMode { get; set; } = string.Empty;

        public bool IsDefault { get; set; }
    }

    internal readonly record struct CopilotBackendMergeResult(
        int Added,
        int Updated,
        int Removed,
        string DefaultLocalProfileId);

    internal sealed class CopilotBackendSyncClient
    {
        private const int MaxResponseBytes = 2 * 1024 * 1024;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.Strict,
        };
        private static readonly HttpMessageHandler SharedHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };

        private readonly HttpClient _httpClient;

        public CopilotBackendSyncClient()
            : this(new HttpClient(SharedHandler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(30),
            })
        {
        }

        internal CopilotBackendSyncClient(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<CopilotBackendConfigResponse> FetchAsync(
            string baseUrl,
            string accessToken,
            bool allowInsecureHttp,
            CancellationToken cancellationToken)
        {
            var endpoint = BuildEndpoint(baseUrl, allowInsecureHttp);
            var token = (accessToken ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Enter the backend sync API Key before syncing.");

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.UserAgent.ParseAdd("ColorVision-Copilot/1.0");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            var json = await ReadLimitedStringAsync(response, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(BuildErrorMessage(response.StatusCode, json));

            CopilotBackendConfigResponse result;
            try
            {
                result = JsonSerializer.Deserialize<CopilotBackendConfigResponse>(json, JsonOptions)
                    ?? throw new JsonException("The response was empty.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("The backend returned an invalid Copilot configuration response.", ex);
            }

            if (result.SchemaVersion != 1)
                throw new InvalidOperationException($"Unsupported backend Copilot schema version {result.SchemaVersion}.");
            result.Profiles ??= new List<CopilotBackendProfile>();
            return result;
        }

        internal static Uri BuildEndpoint(string baseUrl, bool allowInsecureHttp)
        {
            var normalized = (baseUrl ?? string.Empty).Trim();
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out var baseUri)
                || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp)
                || string.IsNullOrWhiteSpace(baseUri.Host))
            {
                throw new InvalidOperationException("Backend URL must be an absolute HTTP or HTTPS address.");
            }
            if (!string.IsNullOrWhiteSpace(baseUri.UserInfo)
                || !string.IsNullOrWhiteSpace(baseUri.Query)
                || !string.IsNullOrWhiteSpace(baseUri.Fragment))
            {
                throw new InvalidOperationException("Backend URL cannot contain credentials, a query, or a fragment.");
            }

            var isLoopback = string.Equals(baseUri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(baseUri.Host, out var ipAddress) && IPAddress.IsLoopback(ipAddress));
            if (baseUri.Scheme == Uri.UriSchemeHttp && !isLoopback && !allowInsecureHttp)
            {
                throw new InvalidOperationException(
                    "Remote HTTP sync is blocked because the sync API Key and model API keys would be sent without transport encryption. Use HTTPS, or explicitly allow insecure HTTP for a trusted network.");
            }

            var builder = new UriBuilder(baseUri)
            {
                Path = "/api/copilot/config",
                Query = string.Empty,
                Fragment = string.Empty,
            };
            return builder.Uri;
        }

        internal static CopilotBackendMergeResult MergeProfiles(
            ObservableCollection<CopilotProfileConfig> profiles,
            CopilotBackendConfigResponse response,
            string baseUrl)
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(response);

            var source = BuildEndpoint(baseUrl, allowInsecureHttp: true).GetLeftPart(UriPartial.Authority).TrimEnd('/');
            var incoming = new Dictionary<string, CopilotProfileConfig>(StringComparer.Ordinal);
            foreach (var item in response.Profiles ?? new List<CopilotBackendProfile>())
            {
                var remoteId = (item.Id ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(remoteId))
                    throw new InvalidOperationException("The backend returned a profile without an id.");
                if (incoming.ContainsKey(remoteId))
                    throw new InvalidOperationException($"The backend returned duplicate profile id '{remoteId}'.");

                incoming.Add(remoteId, CreateProfile(item, source, remoteId));
            }

            var existingByRemoteId = profiles
                .Where(profile => string.Equals(profile.SyncSource, source, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(profile.SyncProfileId))
                .GroupBy(profile => profile.SyncProfileId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var added = 0;
            var updated = 0;
            foreach (var pair in incoming)
            {
                if (existingByRemoteId.TryGetValue(pair.Key, out var existing))
                {
                    CopyProfile(pair.Value, existing);
                    updated++;
                }
                else
                {
                    profiles.Add(pair.Value);
                    added++;
                }
            }

            var staleProfiles = profiles
                .Where(profile => string.Equals(profile.SyncSource, source, StringComparison.OrdinalIgnoreCase)
                    && !incoming.ContainsKey(profile.SyncProfileId))
                .ToArray();
            foreach (var staleProfile in staleProfiles)
                profiles.Remove(staleProfile);

            var defaultRemoteId = (response.DefaultProfileId ?? string.Empty).Trim();
            var defaultLocalProfileId = profiles.FirstOrDefault(profile =>
                string.Equals(profile.SyncSource, source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(profile.SyncProfileId, defaultRemoteId, StringComparison.Ordinal))?.Id ?? string.Empty;

            return new CopilotBackendMergeResult(added, updated, staleProfiles.Length, defaultLocalProfileId);
        }

        private static CopilotProfileConfig CreateProfile(CopilotBackendProfile item, string source, string remoteId)
        {
            if (!Enum.TryParse<CopilotVendorType>(item.VendorType, ignoreCase: true, out var vendorType)
                || !Enum.IsDefined(vendorType))
            {
                throw new InvalidOperationException($"Profile '{remoteId}' has an invalid vendorType.");
            }
            if (!Enum.TryParse<CopilotProviderType>(item.ProviderType, ignoreCase: true, out var providerType)
                || !Enum.IsDefined(providerType))
            {
                throw new InvalidOperationException($"Profile '{remoteId}' has an invalid providerType.");
            }
            if (!Enum.TryParse<CopilotReasoningMode>(item.ReasoningMode, ignoreCase: true, out var reasoningMode)
                || !Enum.IsDefined(reasoningMode))
            {
                throw new InvalidOperationException($"Profile '{remoteId}' has an invalid reasoningMode.");
            }

            var profile = new CopilotProfileConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = item.Name,
                VendorType = vendorType,
                ProviderType = providerType,
                ApiKey = item.ApiKey,
                BaseUrl = item.BaseUrl,
                AllowInsecureHttp = item.AllowInsecureHttp,
                Model = item.Model,
                ReasoningMode = reasoningMode,
                SyncSource = source,
                SyncProfileId = remoteId,
            };
            profile.EnsureValid();
            if (!profile.IsConfigured)
                throw new InvalidOperationException($"Backend profile '{profile.DisplayLabel}' is incomplete or has an invalid endpoint.");
            return profile;
        }

        private static void CopyProfile(CopilotProfileConfig source, CopilotProfileConfig target)
        {
            var localId = target.Id;
            target.VendorType = source.VendorType;
            target.Name = source.Name;
            target.ProviderType = source.ProviderType;
            target.ApiKey = source.ApiKey;
            target.BaseUrl = source.BaseUrl;
            target.AllowInsecureHttp = source.AllowInsecureHttp;
            target.Model = source.Model;
            target.ReasoningMode = source.ReasoningMode;
            target.SyncSource = source.SyncSource;
            target.SyncProfileId = source.SyncProfileId;
            target.Id = localId;
        }

        private static async Task<string> ReadLimitedStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentLength > MaxResponseBytes)
                throw new InvalidOperationException("The backend Copilot configuration response is too large.");

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                var count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                    break;
                if (output.Length + count > MaxResponseBytes)
                    throw new InvalidOperationException("The backend Copilot configuration response is too large.");
                output.Write(buffer, 0, count);
            }
            return Encoding.UTF8.GetString(output.ToArray());
        }

        private static string BuildErrorMessage(HttpStatusCode statusCode, string responseBody)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(error.GetString()))
                {
                    return $"Backend sync failed ({(int)statusCode}): {error.GetString()}";
                }
            }
            catch (JsonException)
            {
            }
            return $"Backend sync failed with HTTP {(int)statusCode}.";
        }
    }
}

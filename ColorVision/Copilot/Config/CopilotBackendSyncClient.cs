using ColorVision.UI;
using ColorVision.UI.CUDA;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotBackendDeviceIdentity(
        string Product,
        string AppVersion,
        string DeviceId,
        string OsVersion,
        string Architecture,
        string VersionKey)
    {
        public static CopilotBackendDeviceIdentity CreateCurrent()
        {
            var appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? string.Empty;
            return new CopilotBackendDeviceIdentity(
                "ColorVision",
                appVersion,
                SystemHelper.GetHardwareId(),
                Environment.OSVersion.Version.ToString(),
                RuntimeInformation.ProcessArchitecture.ToString(),
                DownloadFileConfig.Instance.Authorization);
        }
    }

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

        public bool SupportsImageInput { get; set; }

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
        private static readonly HttpMessageHandler SharedHandler = CreateTransportHandler(useProxy: true);
        private static readonly HttpMessageHandler LoopbackHandler = CreateTransportHandler(useProxy: false);

        private readonly HttpClient _httpClient;
        private readonly HttpClient _loopbackHttpClient;
        private readonly Func<CopilotBackendDeviceIdentity> _deviceIdentityProvider;

        public CopilotBackendSyncClient()
            : this(
                CreateHttpClient(SharedHandler),
                CreateHttpClient(LoopbackHandler),
                CopilotBackendDeviceIdentity.CreateCurrent)
        {
        }

        internal CopilotBackendSyncClient(HttpClient httpClient)
            : this(httpClient, httpClient, CopilotBackendDeviceIdentity.CreateCurrent)
        {
        }

        internal CopilotBackendSyncClient(
            HttpClient httpClient,
            Func<CopilotBackendDeviceIdentity> deviceIdentityProvider)
            : this(httpClient, httpClient, deviceIdentityProvider)
        {
        }

        internal CopilotBackendSyncClient(
            HttpClient httpClient,
            HttpClient loopbackHttpClient,
            Func<CopilotBackendDeviceIdentity> deviceIdentityProvider)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _loopbackHttpClient = loopbackHttpClient ?? throw new ArgumentNullException(nameof(loopbackHttpClient));
            _deviceIdentityProvider = deviceIdentityProvider ?? throw new ArgumentNullException(nameof(deviceIdentityProvider));
        }

        public async Task<CopilotBackendConfigResponse> FetchAsync(
            string baseUrl,
            CancellationToken cancellationToken)
        {
            var endpoint = BuildEndpoint(baseUrl);
            var identity = NormalizeIdentity(_deviceIdentityProvider());
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.ParseAdd("ColorVision-Copilot/1.0");
            AddDeviceProofHeaders(request, identity, timestamp, nonce);

            var httpClient = endpoint.Scheme == Uri.UriSchemeHttp
                ? _loopbackHttpClient
                : _httpClient;
            using var response = await httpClient.SendAsync(
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

        internal static string CreateDeviceSignature(
            CopilotBackendDeviceIdentity identity,
            string timestamp,
            string nonce)
        {
            identity = NormalizeIdentity(identity);
            var canonical = string.Join('\n',
                identity.Product,
                identity.AppVersion,
                identity.DeviceId,
                identity.OsVersion,
                identity.Architecture,
                NormalizeHeaderValue(timestamp, "timestamp"),
                NormalizeHeaderValue(nonce, "nonce"));
            var signature = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(identity.VersionKey),
                Encoding.UTF8.GetBytes(canonical));
            return Convert.ToHexString(signature).ToLowerInvariant();
        }

        private static void AddDeviceProofHeaders(
            HttpRequestMessage request,
            CopilotBackendDeviceIdentity identity,
            string timestamp,
            string nonce)
        {
            request.Headers.TryAddWithoutValidation("X-ColorVision-Product", identity.Product);
            request.Headers.TryAddWithoutValidation("X-ColorVision-Version", identity.AppVersion);
            request.Headers.TryAddWithoutValidation("X-ColorVision-Device-Id", identity.DeviceId);
            request.Headers.TryAddWithoutValidation("X-ColorVision-OS-Version", identity.OsVersion);
            request.Headers.TryAddWithoutValidation("X-ColorVision-Architecture", identity.Architecture);
            request.Headers.TryAddWithoutValidation("X-ColorVision-Timestamp", timestamp);
            request.Headers.TryAddWithoutValidation("X-ColorVision-Nonce", nonce);
            request.Headers.TryAddWithoutValidation(
                "X-ColorVision-Signature",
                CreateDeviceSignature(identity, timestamp, nonce));
        }

        private static CopilotBackendDeviceIdentity NormalizeIdentity(CopilotBackendDeviceIdentity? identity)
        {
            if (identity == null)
                throw new InvalidOperationException("ColorVision could not read the local device identity.");

            var normalized = new CopilotBackendDeviceIdentity(
                NormalizeHeaderValue(identity.Product, "product"),
                NormalizeHeaderValue(identity.AppVersion, "application version"),
                NormalizeHeaderValue(identity.DeviceId, "device id"),
                NormalizeHeaderValue(identity.OsVersion, "OS version"),
                NormalizeHeaderValue(identity.Architecture, "architecture"),
                (identity.VersionKey ?? string.Empty).Trim());
            if (string.IsNullOrWhiteSpace(normalized.VersionKey))
                throw new InvalidOperationException("The installed ColorVision version key is missing.");
            if (string.Equals(normalized.DeviceId, "Unavailable", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ColorVision could not identify this device.");
            return normalized;
        }

        private static string NormalizeHeaderValue(string? value, string name)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                throw new InvalidOperationException($"The local ColorVision {name} is missing.");
            if (normalized.Length > 256
                || normalized.Contains('\r')
                || normalized.Contains('\n'))
            {
                throw new InvalidOperationException($"The local ColorVision {name} is invalid.");
            }
            return normalized;
        }

        internal static Uri BuildEndpoint(string baseUrl)
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

            var normalizedHost = baseUri.IdnHost.TrimEnd('.');
            var isLoopback = IsLoopbackHost(normalizedHost);
            if (baseUri.Scheme == Uri.UriSchemeHttp && !isLoopback)
            {
                throw new InvalidOperationException(
                    "Remote backend sync requires HTTPS because managed API keys and profile settings are security-sensitive. HTTP is allowed only for a loopback service on this computer.");
            }

            var builder = new UriBuilder(baseUri)
            {
                Path = "/api/copilot/config",
                Query = string.Empty,
                Fragment = string.Empty,
            };
            if (isLoopback)
                builder.Host = normalizedHost;
            return builder.Uri;
        }

        internal static bool TryBuildEndpoint(
            string? baseUrl,
            out Uri? endpoint,
            out string errorMessage)
        {
            try
            {
                endpoint = BuildEndpoint(baseUrl ?? string.Empty);
                errorMessage = string.Empty;
                return true;
            }
            catch (InvalidOperationException ex)
            {
                endpoint = null;
                errorMessage = ex.Message;
                return false;
            }
        }

        internal static bool IsTrustedSyncSource(string? source) =>
            !string.IsNullOrWhiteSpace(source)
            && TryBuildEndpoint(source, out _, out _);

        internal static HttpClientHandler CreateTransportHandler(bool useProxy) =>
            new()
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                UseCookies = false,
                UseProxy = useProxy,
            };

        internal static CopilotBackendMergeResult MergeProfiles(
            ObservableCollection<CopilotProfileConfig> profiles,
            CopilotBackendConfigResponse response,
            string baseUrl)
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(response);

            var source = BuildEndpoint(baseUrl).GetLeftPart(UriPartial.Authority).TrimEnd('/');
            var incoming = new Dictionary<string, CopilotProfileConfig>(StringComparer.Ordinal);
            var incomingRemoteIds = new List<string>();
            foreach (var item in response.Profiles ?? new List<CopilotBackendProfile>())
            {
                if (item == null)
                    throw new InvalidOperationException("The backend returned a null profile entry.");
                var remoteId = (item.Id ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(remoteId))
                    throw new InvalidOperationException("The backend returned a profile without an id.");
                if (incoming.ContainsKey(remoteId))
                    throw new InvalidOperationException($"The backend returned duplicate profile id '{remoteId}'.");

                incoming.Add(remoteId, CreateProfile(item, source, remoteId));
                incomingRemoteIds.Add(remoteId);
            }

            var existingGroups = profiles
                .Where(profile => string.Equals(profile.SyncSource, source, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(profile.SyncProfileId))
                .GroupBy(profile => profile.SyncProfileId, StringComparer.Ordinal)
                .ToArray();
            var existingByRemoteId = existingGroups
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var duplicateProfiles = existingGroups
                .SelectMany(group => group.Skip(1))
                .ToHashSet();

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
                    && (!incoming.ContainsKey(profile.SyncProfileId)
                        || duplicateProfiles.Contains(profile)))
                .ToArray();
            foreach (var staleProfile in staleProfiles)
                profiles.Remove(staleProfile);

            ReorderManagedProfiles(profiles, incomingRemoteIds, source);

            var defaultRemoteId = (response.DefaultProfileId ?? string.Empty).Trim();
            var defaultLocalProfileId = profiles.FirstOrDefault(profile =>
                string.Equals(profile.SyncSource, source, StringComparison.OrdinalIgnoreCase)
                && string.Equals(profile.SyncProfileId, defaultRemoteId, StringComparison.Ordinal))?.Id ?? string.Empty;

            return new CopilotBackendMergeResult(added, updated, staleProfiles.Length, defaultLocalProfileId);
        }

        private static void ReorderManagedProfiles(
            ObservableCollection<CopilotProfileConfig> profiles,
            IEnumerable<string> remoteIds,
            string source)
        {
            var managedProfiles = profiles
                .Where(profile => string.Equals(profile.SyncSource, source, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(profile.SyncProfileId))
                .ToArray();
            if (managedProfiles.Length == 0)
                return;

            var managedByRemoteId = managedProfiles.ToDictionary(
                profile => profile.SyncProfileId,
                StringComparer.Ordinal);
            var orderedManagedProfiles = remoteIds
                .Where(managedByRemoteId.ContainsKey)
                .Select(remoteId => managedByRemoteId[remoteId])
                .ToArray();
            var insertionIndex = profiles.IndexOf(managedProfiles[0]);
            foreach (var profile in managedProfiles)
                profiles.Remove(profile);
            foreach (var profile in orderedManagedProfiles)
                profiles.Insert(insertionIndex++, profile);
        }

        private static CopilotProfileConfig CreateProfile(CopilotBackendProfile item, string source, string remoteId)
        {
            var apiKey = (item.ApiKey ?? string.Empty).Trim();
            if (CopilotCredentialProtector.IsProtected(apiKey))
            {
                throw new InvalidOperationException(
                    $"Backend profile '{remoteId}' contains a local protected-credential envelope instead of a transport API key.");
            }
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
                ApiKey = apiKey,
                BaseUrl = item.BaseUrl,
                AllowInsecureHttp = false,
                Model = item.Model,
                SupportsImageInput = item.SupportsImageInput,
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
            target.SupportsImageInput = source.SupportsImageInput;
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

        private static HttpClient CreateHttpClient(HttpMessageHandler handler) =>
            new(handler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(30),
            };

        private static bool IsLoopbackHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || (IPAddress.TryParse(host, out var ipAddress)
                    && IPAddress.IsLoopback(ipAddress));
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

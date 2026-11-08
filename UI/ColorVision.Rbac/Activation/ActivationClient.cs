using ColorVision.UI.Marketplace;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Rbac.Activation;

internal sealed class ActivationClient
{
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(2);
    private readonly ActivationTransport _transport;
    private readonly string _scope;
    private readonly ActivationStore _store;

    private ActivationClient(Uri origin, byte[]? pinnedPublicKey = null)
    {
        _transport = new ActivationTransport(origin, pinnedPublicKey);
        _scope = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(origin.AbsoluteUri)));
        _store = new ActivationStore(_scope);
    }

    public static ActivationClient CreateDefault()
    {
        string? configured = Environment.GetEnvironmentVariable("COLORVISION_ACTIVATION_URL");
        var canonical = new Uri(MarketplaceConfig.ServiceBaseUrl);
        if (string.IsNullOrWhiteSpace(configured))
        {
            if (canonical.Scheme == Uri.UriSchemeHttps)
                return new ActivationClient(canonical);
            if (ActivationTransport.ReadBundledPin() is { } bundledPin)
                return new ActivationClient(canonical, bundledPin);

            var secure = new UriBuilder(canonical) { Scheme = Uri.UriSchemeHttps, Port = -1 };
            return new ActivationClient(secure.Uri);
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
            || origin.AbsolutePath != "/" || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0)
            throw new ActivationException("invalid_server_url");
        byte[]? pin = ActivationTransport.ReadOverridePin() ?? (origin == canonical ? ActivationTransport.ReadBundledPin() : null);
        if (origin.Scheme == Uri.UriSchemeHttp && !origin.IsLoopback && pin is null)
            throw new ActivationException("http_public_key_required");
        return new ActivationClient(origin, pin);
    }

    public Task<ActivationState> RefreshAsync(CancellationToken token) =>
        RunAsync("status", null, null, token);

    public Task<ActivationState> ApplyAsync(ActivationMode mode, CancellationToken token) =>
        RunAsync("apply", null, mode, token);

    public Task<ActivationState> ActivateAsync(string licenseCode, ActivationMode mode, CancellationToken token) =>
        RunAsync("activate", licenseCode.Trim(), mode, token);

    public Task<ActivationState> RenewAsync(CancellationToken token) =>
        RunAsync("renew", null, null, token);

    public Task<ActivationState> ReleaseAsync(CancellationToken token) =>
        RunAsync("release", null, null, token);

    private Task<ActivationState> RunAsync(string action, string? licenseCode, ActivationMode? mode, CancellationToken token) =>
        Task.Run(async () =>
        {
            using var guard = await _store.AcquireAsync(token).ConfigureAwait(false);
            return await ExecuteAsync(action, licenseCode, mode, token).ConfigureAwait(false);
        }, token);

    private async Task<ActivationState> ExecuteAsync(string action, string? licenseCode, ActivationMode? mode, CancellationToken token)
    {
        ActivationCache? cache = _store.Load();
        using ActivationDeviceKey? key = SelectKey(cache, mode);
        if (key is null)
        {
            if (action == "status")
                return new ActivationState { UnavailableReason = cache is null ? null : "device_key_unavailable" };
            throw new ActivationException("not_enrolled");
        }

        try
        {
            var challenge = await _transport.PostAsync<ChallengeResponse>("challenge", new
            {
                Action = action,
                key.PublicKey,
                LicenseCode = licenseCode,
                Mode = mode,
                DeviceName = mode is null ? null : Environment.MachineName
            }, token).ConfigureAwait(false);

            if (string.IsNullOrEmpty(challenge.ChallengeId) || string.IsNullOrEmpty(challenge.Challenge))
                throw new ActivationException("invalid_response");
            byte[] nonce = Convert.FromBase64String(challenge.Challenge);
            if (!IsActivationChallenge(nonce))
                throw new ActivationException("invalid_response");

            string signature = Convert.ToBase64String(key.Sign(nonce));
            if (mode is not null && cache?.DeviceId != key.DeviceId)
            {
                // Retain the signer selection if the server commits and the response is interrupted.
                _store.Save(new ActivationCache(key.HardwareBacked, null, key.DeviceId, null, null, 0, null, null, default));
            }
            else if (action == "release" && cache?.Grant is not null)
            {
                // Once a release may have reached the server, require an authoritative refresh.
                _store.Save(cache with { ReleasePending = true });
            }

            var response = await _transport.PostAsync<DeviceResponse>(action, new
            {
                challenge.ChallengeId,
                Signature = signature
            }, token).ConfigureAwait(false);

            return AcceptResponse(response, key);
        }
        catch (ActivationException ex) when (action == "status" && ex.Code == "not_enrolled")
        {
            _store.Clear();
            return new ActivationState { IsServerAvailable = true };
        }
        catch (ActivationException ex) when (action == "status" && ex.Code == "server_unavailable")
        {
            return ReadOffline(cache, key);
        }
        catch (CryptographicException ex)
        {
            throw new ActivationException("device_key_unavailable", ex);
        }
        catch (FormatException ex)
        {
            throw new ActivationException("invalid_response", ex);
        }
    }

    private static bool IsActivationChallenge(ReadOnlySpan<byte> nonce)
    {
        ReadOnlySpan<byte> prefix = "ColorVision Activation v1\0"u8;
        return nonce.Length == prefix.Length + 32 && nonce.StartsWith(prefix);
    }

    private ActivationDeviceKey? SelectKey(ActivationCache? cache, ActivationMode? mode)
    {
        try
        {
            if (cache is not null)
            {
                if (mode == ActivationMode.Offline && !cache.HardwareBacked)
                {
                    if (cache.EnrollmentMode is not null)
                        throw new ActivationException("release_required");
                }
                else if (ActivationDeviceKey.TryOpen(_scope, cache.HardwareBacked) is { } remembered)
                {
                    if (remembered.DeviceId == cache.DeviceId)
                        return remembered;
                    remembered.Dispose();
                }
                if (cache.EnrollmentMode is not null)
                    throw new ActivationException("device_key_unavailable");
            }

            return mode switch
            {
                ActivationMode.Offline => ActivationDeviceKey.OpenOrCreate(_scope, hardware: true),
                ActivationMode.Online => OpenPlatformKey(create: true) ?? ActivationDeviceKey.OpenOrCreate(_scope, hardware: false),
                _ => OpenPlatformKey(create: false) ?? ActivationDeviceKey.TryOpen(_scope, hardware: false)
            };
        }
        catch (CryptographicException ex)
        {
            throw new ActivationException(mode == ActivationMode.Offline ? "tpm_unavailable" : "device_key_unavailable", ex);
        }
    }

    private ActivationDeviceKey? OpenPlatformKey(bool create)
    {
        try
        {
            return create
                ? ActivationDeviceKey.OpenOrCreate(_scope, hardware: true)
                : ActivationDeviceKey.TryOpen(_scope, hardware: true);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private ActivationState AcceptResponse(DeviceResponse response, ActivationDeviceKey key)
    {
        if (response.CurrentDeviceId != key.DeviceId || response.DeviceLimit < 1 || response.Devices is null
            || response.ServerTime == default || response.LicenseExpiresAt == default)
            throw new ActivationException("invalid_response");

        ActivationDevice? current = response.Devices.Find(device => device.DeviceId == key.DeviceId);
        DateTimeOffset? offlineExpires = null;
        if (response.Grant is not null)
        {
            var claims = VerifyGrant(response.Grant, key);
            if (claims is null || current?.Mode != ActivationMode.Offline
                || claims.IssuedAt > response.ServerTime || claims.ExpiresAt <= response.ServerTime
                || claims.ExpiresAt != current.ExpiresAt || claims.ExpiresAt > response.LicenseExpiresAt)
                throw new ActivationException("invalid_response");
            offlineExpires = claims.ExpiresAt;
        }

        _store.Save(new ActivationCache(key.HardwareBacked, response.EnrollmentMode, key.DeviceId, response.LicenseCode,
            response.LicenseExpiresAt, response.DeviceLimit, current, response.Grant, response.ServerTime));
        return new ActivationState
        {
            LicenseCode = response.LicenseCode,
            LicenseExpiresAt = response.LicenseExpiresAt,
            CurrentDeviceId = key.DeviceId,
            DeviceLimit = response.DeviceLimit,
            Devices = response.Devices,
            EnrollmentMode = response.EnrollmentMode,
            OfflineExpiresAt = offlineExpires,
            IsServerAvailable = true
        };
    }

    private ActivationState ReadOffline(ActivationCache? cache, ActivationDeviceKey key)
    {
        var unavailable = new ActivationState { UnavailableReason = "server_unavailable" };
        if (cache?.DeviceId != key.DeviceId)
            return unavailable;

        unavailable = unavailable with { CurrentDeviceId = cache.DeviceId, EnrollmentMode = cache.EnrollmentMode };
        if (cache.ReleasePending || cache.Device is null || cache.Grant is null)
            return unavailable;
        var claims = VerifyGrant(cache.Grant, key);
        if (claims is null)
            return unavailable;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset lastObserved = cache.LastObservedAt > claims.IssuedAt ? cache.LastObservedAt : claims.IssuedAt;
        if (lastObserved - now > ClockTolerance)
            return unavailable with { UnavailableReason = "offline_clock_invalid" };
        DateTimeOffset observed = now > lastObserved ? now : lastObserved;

        // Persist the greatest observed time so routine local clock rollback cannot extend the grant.
        if (observed > cache.LastObservedAt)
            _store.Save(cache with { LastObservedAt = observed });
        if (observed >= claims.ExpiresAt)
            return unavailable;
        try
        {
            if (!key.CanSign())
                return unavailable with { UnavailableReason = "device_key_unavailable" };
        }
        catch (CryptographicException)
        {
            return unavailable with { UnavailableReason = "device_key_unavailable" };
        }

        return new ActivationState
        {
            LicenseCode = cache.LicenseCode,
            LicenseExpiresAt = cache.LicenseExpiresAt,
            CurrentDeviceId = key.DeviceId,
            DeviceLimit = cache.DeviceLimit,
            Devices = [cache.Device],
            EnrollmentMode = ActivationMode.Offline,
            OfflineExpiresAt = claims.ExpiresAt,
            IsOfflineSnapshot = true,
            UnavailableReason = "server_unavailable"
        };
    }

    private static GrantClaims? VerifyGrant(ActivationGrant grant, ActivationDeviceKey key)
    {
        if (!key.HardwareBacked || string.IsNullOrEmpty(grant.PublicKey)
            || string.IsNullOrEmpty(grant.Payload) || string.IsNullOrEmpty(grant.Signature))
            return null;

        try
        {
            using var signer = ECDsa.Create();
            byte[] publicKey = Convert.FromBase64String(grant.PublicKey);
            signer.ImportSubjectPublicKeyInfo(publicKey, out int read);
            byte[] payload = Convert.FromBase64String(grant.Payload);
            if (read != publicKey.Length || signer.KeySize != 256
                || !signer.VerifyData(payload, Convert.FromBase64String(grant.Signature), HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                return null;

            var claims = JsonSerializer.Deserialize<GrantClaims>(payload, ActivationTransport.JsonOptions);
            return claims is { Version: 1, Mode: ActivationMode.Offline }
                && claims.DeviceId == key.DeviceId && claims.IssuedAt != default && claims.ExpiresAt > claims.IssuedAt
                ? claims : null;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    private sealed record ChallengeResponse(string ChallengeId, string Challenge);
    private sealed record GrantClaims(int Version, string DeviceId, ActivationMode? Mode, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
    private sealed record DeviceResponse(string? LicenseCode, DateTimeOffset LicenseExpiresAt, int DeviceLimit,
        string CurrentDeviceId, List<ActivationDevice> Devices, ActivationGrant? Grant, DateTimeOffset ServerTime,
        ActivationMode? EnrollmentMode);
}

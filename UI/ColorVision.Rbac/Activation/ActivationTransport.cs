using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Rbac.Activation;

internal sealed class ActivationTransport(Uri origin, byte[]? pinnedPublicKey)
{
    private const string PinResource = "ColorVision.Rbac.Activation.HttpPublicKey.der";
    private const int MaximumPinBytes = 4096;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private static ReadOnlySpan<byte> RequestContext => "ColorVision Activation HTTP v1 request"u8;
    private static ReadOnlySpan<byte> ResponseContext => "ColorVision Activation HTTP v1 response"u8;

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter<ActivationMode>(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) }
    };

    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10),
        MaxResponseContentBufferSize = 64 * 1024
    };

    public static byte[]? ReadBundledPin()
    {
        using Stream? stream = typeof(ActivationTransport).Assembly.GetManifestResourceStream(PinResource);
        if (stream is null)
            return null;
        if (stream.Length is <= 0 or > MaximumPinBytes)
            throw new ActivationException("invalid_http_public_key");
        byte[] pin = new byte[stream.Length];
        stream.ReadExactly(pin);
        return ValidatePin(pin);
    }

    public static byte[]? ReadOverridePin()
    {
        string? value = Environment.GetEnvironmentVariable("COLORVISION_ACTIVATION_HTTP_PUBLIC_KEY");
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (value.Length > MaximumPinBytes * 2)
            throw new ActivationException("invalid_http_public_key");
        try
        {
            return ValidatePin(Convert.FromBase64String(value));
        }
        catch (FormatException ex)
        {
            throw new ActivationException("invalid_http_public_key", ex);
        }
    }

    private static byte[] ValidatePin(byte[] pin)
    {
        if (pin.Length is <= 0 or > MaximumPinBytes)
            throw new ActivationException("invalid_http_public_key");
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(pin, out int read);
            if (read != pin.Length || rsa.KeySize < 3072)
                throw new ActivationException("invalid_http_public_key");
            return pin;
        }
        catch (CryptographicException ex)
        {
            throw new ActivationException("invalid_http_public_key", ex);
        }
    }

    public async Task<T> PostAsync<T>(string action, object payload, CancellationToken token) where T : class
    {
        try
        {
            return pinnedPublicKey is null
                ? await PostDirectAsync<T>(action, payload, token).ConfigureAwait(false)
                : await PostProtectedAsync<T>(action, payload, pinnedPublicKey, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            throw new ActivationException("server_unavailable", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new ActivationException("server_unavailable", ex);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            throw new ActivationException("invalid_response", ex);
        }
    }

    private async Task<T> PostDirectAsync<T>(string action, object payload, CancellationToken token) where T : class
    {
        using var response = await Http.PostAsJsonAsync(new Uri(origin, "api/activation/" + action), payload, JsonOptions, token)
            .ConfigureAwait(false);
        if ((int)response.StatusCode >= 500)
            throw new ActivationException("server_unavailable");
        if (!response.IsSuccessStatusCode)
        {
            if (response.Content.Headers.ContentType?.MediaType != "application/json")
                throw new ActivationException("server_unavailable");
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, token).ConfigureAwait(false);
            throw new ActivationException(error?.Error ?? "invalid_response");
        }
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, token).ConfigureAwait(false)
            ?? throw new ActivationException("invalid_response");
    }

    private async Task<T> PostProtectedAsync<T>(string action, object payload, byte[] pin, CancellationToken token) where T : class
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(pin, out _);
        byte[] keys = RandomNumberGenerator.GetBytes(2 * KeySize);
        try
        {
            var request = ProtectRequest(rsa, keys, action, payload);
            using var response = await Http.PostAsJsonAsync(new Uri(origin, "api/activation/secure"), request, JsonOptions, token)
                .ConfigureAwait(false);
            if ((int)response.StatusCode >= 500)
                throw new ActivationException("server_unavailable");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ActivationException("rate_limited");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new ActivationException("invalid_response");

            var envelope = await response.Content.ReadFromJsonAsync<ProtectedResponse>(JsonOptions, token).ConfigureAwait(false)
                ?? throw new ActivationException("invalid_response");
            return ReadProtectedResponse<T>(envelope, keys.AsSpan(KeySize, KeySize));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keys);
        }
    }

    private static ProtectedRequest ProtectRequest(RSA rsa, byte[] keys, string action, object payload)
    {
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new { Action = action, Payload = payload }, JsonOptions);
        try
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] combined = new byte[plain.Length + TagSize];
            using var gcm = new AesGcm(keys.AsSpan(0, KeySize), TagSize);
            gcm.Encrypt(nonce, plain, combined.AsSpan(0, plain.Length), combined.AsSpan(plain.Length), RequestContext);
            return new ProtectedRequest(rsa.Encrypt(keys, RSAEncryptionPadding.OaepSHA256), nonce, combined);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private static T ReadProtectedResponse<T>(ProtectedResponse envelope, ReadOnlySpan<byte> key) where T : class
    {
        if (envelope.Nonce is not { Length: NonceSize } || envelope.Ciphertext is null || envelope.Ciphertext.Length <= TagSize)
            throw new ActivationException("invalid_response");

        byte[] plain = new byte[envelope.Ciphertext.Length - TagSize];
        try
        {
            using var gcm = new AesGcm(key, TagSize);
            gcm.Decrypt(envelope.Nonce, envelope.Ciphertext.AsSpan(0, plain.Length),
                envelope.Ciphertext.AsSpan(plain.Length), plain, ResponseContext);

            using var document = JsonDocument.Parse(plain);
            JsonElement body = document.RootElement;
            if (body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("status", out JsonElement statusField) || statusField.ValueKind != JsonValueKind.Number
                || !statusField.TryGetInt32(out int status)
                || !body.TryGetProperty("payload", out JsonElement payload) || payload.ValueKind != JsonValueKind.Object)
                throw new ActivationException("invalid_response");
            if (status == 200)
                return payload.Deserialize<T>(JsonOptions) ?? throw new ActivationException("invalid_response");
            if (status is >= 500 and <= 599)
                throw new ActivationException("server_unavailable");
            if (status is >= 400 and <= 499)
                throw new ActivationException(payload.Deserialize<ErrorResponse>(JsonOptions)?.Error ?? "invalid_response");
            throw new ActivationException("invalid_response");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private sealed record ErrorResponse(string Error);
    private sealed record ProtectedRequest(byte[] Key, byte[] Nonce, byte[] Ciphertext);
    private sealed record ProtectedResponse(byte[] Nonce, byte[] Ciphertext);
}

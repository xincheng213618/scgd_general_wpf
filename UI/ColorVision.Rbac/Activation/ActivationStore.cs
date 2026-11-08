using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ColorVision.Rbac.Activation;

internal sealed record ActivationGrant(string Payload, string Signature, string PublicKey);

internal sealed record ActivationCache(
    bool HardwareBacked, ActivationMode? EnrollmentMode, string DeviceId,
    string? LicenseCode, DateTimeOffset? LicenseExpiresAt, int DeviceLimit,
    ActivationDevice? Device, ActivationGrant? Grant, DateTimeOffset LastObservedAt,
    bool ReleasePending = false);

internal sealed class ActivationStore
{
    private readonly byte[] _entropy;
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Activation");
    private readonly string _path;

    public ActivationStore(string scope)
    {
        _entropy = Convert.FromHexString(scope);
        _path = Path.Combine(_directory, scope + ".bin");
    }

    public async Task<FileStream> AcquireAsync(CancellationToken token)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            long started = Stopwatch.GetTimestamp();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    // Keep status, mutations and their cache writes in order across ColorVision instances.
                    return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException ex) when (ex.HResult == unchecked((int)0x80070020)) // ERROR_SHARING_VIOLATION
                {
                    if (Stopwatch.GetElapsedTime(started) >= TimeSpan.FromSeconds(4))
                        throw new ActivationException("activation_busy", ex);
                    await Task.Delay(100, token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ActivationException("local_storage_error", ex);
        }
    }

    public ActivationCache? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;
            byte[] plaintext = ProtectedData.Unprotect(File.ReadAllBytes(_path), _entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<ActivationCache>(plaintext);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    public void Save(ActivationCache cache)
    {
        string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(_directory);
            byte[] data = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(cache), _entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new ActivationException("local_storage_error", ex);
        }
        finally
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public void Clear()
    {
        try
        {
            File.Delete(_path);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ActivationException("local_storage_error", ex);
        }
    }
}

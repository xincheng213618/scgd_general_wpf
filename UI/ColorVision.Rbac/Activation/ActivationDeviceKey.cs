using System.Security.Cryptography;

namespace ColorVision.Rbac.Activation;

internal sealed class ActivationDeviceKey : IDisposable
{
    private readonly ECDsaCng _signer;

    public bool HardwareBacked { get; }
    public string PublicKey { get; }
    public string DeviceId { get; }

    private ActivationDeviceKey(CngKey key, bool hardwareBacked)
    {
        if (key.AlgorithmGroup != CngAlgorithmGroup.ECDsa || key.KeySize != 256
            || key.IsMachineKey || key.ExportPolicy != CngExportPolicies.None)
            throw new CryptographicException("The activation key does not match the storage policy.");

        _signer = new ECDsaCng(key);
        try
        {
            byte[] publicKey = _signer.ExportSubjectPublicKeyInfo();
            PublicKey = Convert.ToBase64String(publicKey);
            DeviceId = Convert.ToHexStringLower(SHA256.HashData(publicKey));
            HardwareBacked = hardwareBacked;
        }
        catch
        {
            _signer.Dispose();
            throw;
        }
    }

    public static ActivationDeviceKey? TryOpen(string scope, bool hardware)
    {
        var provider = Provider(hardware);
        string name = KeyName(scope, hardware);
        if (!CngKey.Exists(name, provider, CngKeyOpenOptions.Silent))
            return null;

        using var key = CngKey.Open(name, provider, CngKeyOpenOptions.Silent);
        return new ActivationDeviceKey(key, hardware);
    }

    public static ActivationDeviceKey OpenOrCreate(string scope, bool hardware)
    {
        if (TryOpen(scope, hardware) is { } existing)
            return existing;

        var parameters = new CngKeyCreationParameters
        {
            Provider = Provider(hardware),
            ExportPolicy = CngExportPolicies.None,
            KeyUsage = CngKeyUsages.Signing
        };
        string name = KeyName(scope, hardware);
        try
        {
            using var key = CngKey.Create(CngAlgorithm.ECDsaP256, name, parameters);
            return new ActivationDeviceKey(key, hardware);
        }
        catch (CryptographicException ex) when (ex.HResult == unchecked((int)0x8009000F)) // NTE_EXISTS
        {
            if (TryOpen(scope, hardware) is { } concurrent)
                return concurrent;
            throw;
        }
    }

    public byte[] Sign(byte[] challenge) =>
        _signer.SignData(challenge, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

    public bool CanSign()
    {
        byte[] challenge = RandomNumberGenerator.GetBytes(32);
        return _signer.VerifyData(challenge, Sign(challenge), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static CngProvider Provider(bool hardware) =>
        hardware ? CngProvider.MicrosoftPlatformCryptoProvider : CngProvider.MicrosoftSoftwareKeyStorageProvider;

    private static string KeyName(string scope, bool hardware) =>
        $"ColorVision.Activation.v1.{scope}.{(hardware ? "Platform" : "Online")}";

    public void Dispose() => _signer.Dispose();
}

using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using System;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal sealed class LocalSpectrumLicenseException : InvalidOperationException
{
    public LocalSpectrumLicenseException() : base(Properties.Resources.UnauthorizedOrLicenseExpired) { }
}

internal static class LocalSpectrumLicense
{
    internal static LocalSpectrumLicenseSource Capture(DeviceSpectrum owner, Func<bool> ownsConfiguration, string? directory = null)
    {
        int resourceId = owner.SysResourceModel.Id;
        bool skipValidation = ExperimentalFeaturesConfig.Instance.IsEnabled;
        MySqlConnectedEndpoint? endpoint = resourceId > 0 ? owner.ResultDatabaseEndpoint
            : resourceId is 0 or -1 ? MySqlControl.GetInstance().ConnectedEndpoint : null;
        if (resourceId > 0 || endpoint?.IsCurrent == true)
        {
            bool OwnsEndpoint() => ownsConfiguration() && endpoint?.IsCurrent == true
                && (resourceId <= 0 || ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint));
            LicenseModel? FindOnline(string serialNumber)
            {
                using var db = endpoint!.CreateDbClient();
                return PhyLicenseDao.GetByMAC(db, serialNumber);
            }
            return new(OwnsEndpoint, FindOnline, skipValidation, directory);
        }

        SysResourceDao? attachedDao = owner.LocalConfigurationResourceDao;
        SysResourceDao? localDao = attachedDao ?? (SysResourceDao.IsLocalId(resourceId) ? SysResourceDao.Instance : null);
        PhyLicenseDao licenses = localDao == null ? PhyLicenseDao.Instance : new PhyLicenseDao(localDao.LocalResourceStore);
        return new(() => ownsConfiguration() && ReferenceEquals(owner.LocalConfigurationResourceDao, attachedDao),
            serialNumber => licenses.GetByMAC(serialNumber, local: true), skipValidation, directory);
    }

    internal static void ValidateSerialNumber(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || serialNumber.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || serialNumber is "." or "..")
            throw new InvalidOperationException("请先选择有效的光谱仪 SN。");
    }

    internal static void EnsureNotExpired(DateTime? expiryDate, bool skipValidation, DateTime now)
    {
        // Match the expiry displayed by physical spectrum management without decoding or validating the payload.
        if (!skipValidation && (!expiryDate.HasValue || expiryDate.Value <= now))
            throw new LocalSpectrumLicenseException();
    }
}

internal sealed class LocalSpectrumLicenseSource(Func<bool> ownsSource, Func<string, LicenseModel?> findLicense,
    bool skipValidation, string? directory = null)
{
    private readonly string licenseDirectory = directory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license");
    internal bool SkipValidation { get; } = skipValidation;

    internal void EnsureCurrent()
    {
        if (!ownsSource())
            throw new InvalidOperationException("光谱仪原许可证来源已不可用，请重新选择设备后重试。");
    }

    internal DateTime? Prepare(string serialNumber)
    {
        LocalSpectrumLicense.ValidateSerialNumber(serialNumber);
        EnsureCurrent();
        LicenseModel? license = findLicense(serialNumber);
        DateTime? expiry = license is { LiceType: 1 } ? license.ExpiryDate : null;
        string? contents = license is { LiceType: 1 } ? license.LicenseValue : null;
        EnsureCurrent();

        string path = Path.Combine(licenseDirectory, serialNumber + ".lic");
        // A file installed with the product remains usable without a database connection.
        if (string.IsNullOrWhiteSpace(contents))
        {
            if (!File.Exists(path))
                throw new InvalidOperationException("未找到本地光谱仪许可证，请在物理光谱仪管理中导入许可证。");
            EnsureCurrent();
            return expiry;
        }
        if (File.Exists(path) && File.ReadAllText(path) == contents)
        {
            EnsureCurrent();
            return expiry;
        }
        Directory.CreateDirectory(licenseDirectory);
        string temporary = Path.Combine(licenseDirectory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, contents, new UTF8Encoding(false));
            EnsureCurrent();
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return expiry;
    }
}

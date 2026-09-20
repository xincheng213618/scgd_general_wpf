using ColorVision.Engine.Services.PhyCameras.Licenses;
using System;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal static class LocalSpectrumLicense
{
    internal static void EnsureAvailable(string serialNumber, bool localStore)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || serialNumber.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || serialNumber is "." or "..")
            throw new InvalidOperationException("请先选择有效的光谱仪 SN。");
        string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "license");
        string path = Path.Combine(directory, serialNumber + ".lic");
        // A file installed with the product remains usable without a database connection.
        LicenseModel? license = PhyLicenseDao.Instance.GetByMAC(serialNumber, localStore || !ColorVision.Database.MySqlSetting.IsConnect);
        if (license?.LiceType != 1 || string.IsNullOrWhiteSpace(license.LicenseValue))
        {
            if (File.Exists(path)) return;
            throw new InvalidOperationException("未找到本地光谱仪许可证，请在物理光谱仪管理中导入许可证。");
        }
        Directory.CreateDirectory(directory);
        if (File.Exists(path) && File.ReadAllText(path) == license.LicenseValue) return;
        string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, license.LicenseValue, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

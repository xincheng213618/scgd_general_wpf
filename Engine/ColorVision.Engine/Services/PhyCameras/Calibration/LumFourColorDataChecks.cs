using ColorVision.Engine.Media;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    internal static class LumFourColorDataChecks
    {
        // The supplied calibration procedure specifies IP = peak AD / 65535, acceptable from 30% to 95%.
        public static double? IpPercent(double? peakAd) => peakAd / 65535d * 100d;

        public static string? SpectrumWarning(double? peakAd)
        {
            if (!peakAd.HasValue)
                return "缺少峰值 AD，无法判断光谱饱和度";
            double percent = IpPercent(peakAd)!.Value;
            if (!double.IsFinite(percent) || percent < 0 || percent > 100)
                throw new InvalidOperationException("光谱峰值 AD 必须在 0～65535 之间。");
            if (percent < 30)
                return $"IP {percent:F2}% 偏低（要求 30%～95%），请增加积分时间或调整 ND 后重采";
            if (percent > 95)
                return $"IP {percent:F2}% 偏高（要求 30%～95%），请降低积分时间或调整 ND 后重采";
            return null;
        }

        public static void ValidateYxy(ColorCorrectionYxy value, string name)
        {
            if (!double.IsFinite(value.Y) || !double.IsFinite(value.CieX) || !double.IsFinite(value.CieY) || value.CieY == 0)
                throw new InvalidOperationException($"{name}必须是有限数值，且 CIE y 不能为 0。");
            if (!double.IsFinite(value.Y * (value.CieX / value.CieY)) ||
                !double.IsFinite(value.Y * ((1 - value.CieX - value.CieY) / value.CieY)))
                throw new InvalidOperationException($"{name}换算 XYZ 时溢出。");
        }
    }

    internal sealed record LumFourColorSourceSnapshot(string Path, string Hash, CVRawManualCieConfig Config)
    {
        public static string ComputeHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

        public static LumFourColorSourceSnapshot Load(string path)
        {
            string fullPath = System.IO.Path.GetFullPath(path);
            string hash = ComputeHash(fullPath);
            if (!CVRawManualCieCalculator.TryLoadLumFourColorCalibrationDefaults(fullPath, out var config, out string? error))
                throw new InvalidOperationException(error ?? "无法读取原四色校正文件。");
            var result = new LumFourColorSourceSnapshot(fullPath, hash, config);
            result.EnsureUnchanged();
            return result;
        }

        public void EnsureUnchanged()
        {
            if (!File.Exists(Path) || ComputeHash(Path) != Hash)
                throw new InvalidOperationException("原校正文件已被修改或移除，请重新选择文件并采集，避免使用旧数据。");
        }

        public void SaveCopy(string destination, CVRawManualCieConfig corrected)
        {
            string fullPath = System.IO.Path.GetFullPath(destination);
            if (string.Equals(fullPath, Path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("请选择新的文件名，保留原校正文件。");
            EnsureUnchanged();
            string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, LumFourColorCorrectionCalculator.SerializeCalibrationFile(corrected), new UTF8Encoding(false));
                _ = Load(temporary);
                EnsureUnchanged();
                if (File.Exists(fullPath))
                    File.Replace(temporary, fullPath, null);
                else
                    File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }
    }
}

using log4net;
using System;
using System.IO;

namespace cvColorVision
{
    /// <summary>
    /// Validation result for a spectrometer calibration file.
    /// </summary>
    public sealed class SpectrumCalibrationFileValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int DataCount { get; set; }
        public string FileType { get; set; } = string.Empty;
        public float MagExpTime { get; set; }
        public int LvCoefficient { get; set; }
    }

    /// <summary>
    /// Validates the binary calibration formats consumed by the native spectrometer APIs.
    /// </summary>
    public static class SpectrumCalibrationFileValidator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SpectrumCalibrationFileValidator));

        /// <summary>
        /// Validates a wavelength file.
        /// Format: [uint64 DataLength] [double[] wavelengths].
        /// </summary>
        public static SpectrumCalibrationFileValidationResult ValidateWavelengthFile(string filePath, bool logSuccess = false)
        {
            var result = new SpectrumCalibrationFileValidationResult { FileType = "波长标定" };
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.Message = "文件不存在";
                return result;
            }

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileLength = stream.Length;
                if (fileLength < sizeof(ulong))
                {
                    result.Message = $"文件太小 ({fileLength} bytes)，格式不正确";
                    return result;
                }

                using var reader = new BinaryReader(stream);
                ulong dataLength = reader.ReadUInt64();
                if (dataLength < sizeof(ulong) || dataLength != (ulong)fileLength)
                {
                    result.Message = $"文件头DataLength={dataLength}，文件大小={fileLength}，格式不匹配";
                    return result;
                }

                // The native implementation uses (DataLength - 4) / sizeof(double),
                // even though it reads DataLength as uint64. Preserve that behavior.
                ulong count = (dataLength - 4) / sizeof(double);
                if (count > int.MaxValue)
                {
                    result.Message = $"波长数据点数量过大: {count}";
                    return result;
                }

                ulong expectedBytes = count * sizeof(double);
                ulong remainingBytes = (ulong)(fileLength - sizeof(ulong));
                if (remainingBytes < expectedBytes)
                {
                    result.Message = $"数据不足: 期望{count}个波长值({expectedBytes} bytes)，实际剩余{remainingBytes} bytes";
                    return result;
                }

                result.IsValid = true;
                result.DataCount = (int)count;
                result.Message = $"有效: {count} 个波长数据点";
                if (logSuccess)
                    log.Info($"Wavelength file validated: {filePath}, {count} points");
            }
            catch (Exception ex)
            {
                result.Message = $"读取失败: {ex.Message}";
                log.Error($"Failed to validate wavelength file: {filePath}", ex);
            }

            return result;
        }

        /// <summary>
        /// Validates an amplitude (Maguide) file.
        /// Format: [uint64 DataLength] [float MagExpTm] [int LvCoffe] [uint64 nCount]
        /// [double[] wavelengths] [double[] coefficients].
        /// </summary>
        public static SpectrumCalibrationFileValidationResult ValidateMaguideFile(string filePath, bool logSuccess = false)
        {
            var result = new SpectrumCalibrationFileValidationResult { FileType = "幅值标定" };
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                result.Message = "文件不存在";
                return result;
            }

            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileLength = stream.Length;
                const int headerSize = sizeof(ulong) + sizeof(float) + sizeof(int) + sizeof(ulong);
                if (fileLength < headerSize)
                {
                    result.Message = $"文件太小 ({fileLength} bytes)，格式不正确";
                    return result;
                }

                using var reader = new BinaryReader(stream);
                ulong dataLength = reader.ReadUInt64();
                if (dataLength < sizeof(ulong) || dataLength != (ulong)fileLength)
                {
                    result.Message = $"文件头DataLength={dataLength}，文件大小={fileLength}，格式不匹配";
                    return result;
                }

                float magExpTime = reader.ReadSingle();
                int lvCoefficient = reader.ReadInt32();
                ulong count = reader.ReadUInt64();
                if (count > int.MaxValue || count > (ulong)(long.MaxValue / (2 * sizeof(double))))
                {
                    result.Message = $"标定数据点数量过大: {count}";
                    return result;
                }

                long expectedDataBytes = (long)count * 2 * sizeof(double);
                long remainingBytes = fileLength - headerSize;
                if (remainingBytes < expectedDataBytes)
                {
                    result.Message = $"数据不足: 期望{count}个标定点(2×{count}×8={expectedDataBytes} bytes)，实际剩余{remainingBytes} bytes";
                    return result;
                }

                result.IsValid = true;
                result.DataCount = (int)count;
                result.MagExpTime = magExpTime;
                result.LvCoefficient = lvCoefficient;
                result.Message = $"有效: {count} 个标定数据点, 积分时间={magExpTime}ms, Lv系数={lvCoefficient}";
                if (logSuccess)
                    log.Info($"Maguide file validated: {filePath}, {count} points, ExpTime={magExpTime}, LvCoffe={lvCoefficient}");
            }
            catch (Exception ex)
            {
                result.Message = $"读取失败: {ex.Message}";
                log.Error($"Failed to validate maguide file: {filePath}", ex);
            }

            return result;
        }
    }
}

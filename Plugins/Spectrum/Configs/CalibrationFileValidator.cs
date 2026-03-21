using log4net;
using System.IO;

namespace Spectrum.Configs
{
    /// <summary>
    /// Validation result for a calibration file.
    /// </summary>
    public class CalibrationFileValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int DataCount { get; set; }
        public string FileType { get; set; } = string.Empty;

        /// <summary>
        /// For maguide files: the exposure time stored in the file header.
        /// </summary>
        public float MagExpTime { get; set; }

        /// <summary>
        /// For maguide files: the Lv coefficient stored in the file header.
        /// </summary>
        public int LvCoefficient { get; set; }
    }

    /// <summary>
    /// Validates calibration .dat files by reading the binary format used by SpectraBase C++ code.
    /// Wavelength file format: [uint64 DataLength] [double[] wavelengths]
    /// Maguide file format:    [uint64 DataLength] [float MagExpTm] [int LvCoffe] [uint64 nCount] [double[] wavelengths] [double[] coefficients]
    /// </summary>
    public static class CalibrationFileValidator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CalibrationFileValidator));

        /// <summary>
        /// Validates a wavelength calibration file (.dat).
        /// Mirrors SpectraBase::SetWavelengthFile logic.
        /// </summary>
        public static CalibrationFileValidationResult ValidateWavelengthFile(string filePath)
        {
            var result = new CalibrationFileValidationResult { FileType = "波长标定" };

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                result.Message = "文件不存在";
                return result;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileLength = fs.Length;

                if (fileLength < sizeof(ulong))
                {
                    result.Message = $"文件太小 ({fileLength} bytes)，格式不正确";
                    return result;
                }

                using var br = new BinaryReader(fs);
                ulong dataLength = br.ReadUInt64();

                if (dataLength < sizeof(ulong) || dataLength != (ulong)fileLength)
                {
                    result.Message = $"文件头DataLength={dataLength}，文件大小={fileLength}，格式不匹配";
                    return result;
                }

                // nCount = (DataLength - 4) / sizeof(double)
                // Note: C++ code uses "DataLength - 4" which appears to be a legacy quirk (header is 8 bytes for uint64)
                // We keep the same calculation for compatibility
                ulong nCount = (dataLength - 4) / sizeof(double);

                long remainingBytes = fileLength - sizeof(ulong);
                long expectedBytes = (long)(nCount * sizeof(double));

                if (remainingBytes < expectedBytes)
                {
                    result.Message = $"数据不足: 期望{nCount}个波长值({expectedBytes} bytes)，实际剩余{remainingBytes} bytes";
                    return result;
                }

                result.IsValid = true;
                result.DataCount = (int)nCount;
                result.Message = $"有效: {nCount} 个波长数据点";

                log.Info($"Wavelength file validated: {filePath}, {nCount} points");
            }
            catch (Exception ex)
            {
                result.Message = $"读取失败: {ex.Message}";
                log.Error($"Failed to validate wavelength file: {filePath}", ex);
            }

            return result;
        }

        /// <summary>
        /// Validates a maguide (amplitude) calibration file (.dat).
        /// Mirrors SpectraBase::SetMagiudeFile logic.
        /// </summary>
        public static CalibrationFileValidationResult ValidateMaguideFile(string filePath)
        {
            var result = new CalibrationFileValidationResult { FileType = "幅值标定" };

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                result.Message = "文件不存在";
                return result;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                long fileLength = fs.Length;

                // Minimum size: uint64 + float + int + uint64 = 8 + 4 + 4 + 8 = 24 bytes
                if (fileLength < 24)
                {
                    result.Message = $"文件太小 ({fileLength} bytes)，格式不正确";
                    return result;
                }

                using var br = new BinaryReader(fs);
                ulong dataLength = br.ReadUInt64();

                if (dataLength < sizeof(ulong) || dataLength != (ulong)fileLength)
                {
                    result.Message = $"文件头DataLength={dataLength}，文件大小={fileLength}，格式不匹配";
                    return result;
                }

                float dMagExpTm = br.ReadSingle();
                int nLvCoffe = br.ReadInt32();
                ulong nCount = br.ReadUInt64();

                // After header (8+4+4+8=24 bytes), we need nCount*double wavelengths + nCount*double coefficients
                long headerSize = sizeof(ulong) + sizeof(float) + sizeof(int) + sizeof(ulong);
                long expectedDataBytes = (long)(nCount * 2 * sizeof(double));
                long remainingBytes = fileLength - headerSize;

                if (remainingBytes < expectedDataBytes)
                {
                    result.Message = $"数据不足: 期望{nCount}个标定点(2×{nCount}×8={expectedDataBytes} bytes)，实际剩余{remainingBytes} bytes";
                    return result;
                }

                result.IsValid = true;
                result.DataCount = (int)nCount;
                result.MagExpTime = dMagExpTm;
                result.LvCoefficient = nLvCoffe;
                result.Message = $"有效: {nCount} 个标定数据点, 积分时间={dMagExpTm}ms, Lv系数={nLvCoffe}";

                log.Info($"Maguide file validated: {filePath}, {nCount} points, ExpTime={dMagExpTm}, LvCoffe={nLvCoffe}");
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

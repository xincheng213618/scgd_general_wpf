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
        /// <summary>
        /// Validates a wavelength calibration file (.dat).
        /// Mirrors SpectraBase::SetWavelengthFile logic.
        /// </summary>
        public static CalibrationFileValidationResult ValidateWavelengthFile(string filePath)
        {
            return FromSharedResult(cvColorVision.SpectrumCalibrationFileValidator.ValidateWavelengthFile(filePath, logSuccess: true));
        }

        /// <summary>
        /// Validates a maguide (amplitude) calibration file (.dat).
        /// Mirrors SpectraBase::SetMagiudeFile logic.
        /// </summary>
        public static CalibrationFileValidationResult ValidateMaguideFile(string filePath)
        {
            return FromSharedResult(cvColorVision.SpectrumCalibrationFileValidator.ValidateMaguideFile(filePath, logSuccess: true));
        }

        private static CalibrationFileValidationResult FromSharedResult(cvColorVision.SpectrumCalibrationFileValidationResult result) => new()
        {
            IsValid = result.IsValid,
            Message = result.Message,
            DataCount = result.DataCount,
            FileType = result.FileType,
            MagExpTime = result.MagExpTime,
            LvCoefficient = result.LvCoefficient,
        };
    }
}

using ColorVision.UI;
using log4net;
using OpenCvSharp;
using System;
using System.IO;

namespace Conoscope.Core
{
    public sealed class ConoscopeGlobalReferenceStore : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeGlobalReferenceStore));
        private readonly ConoscopeConfig config;

        private Mat? colorDifferenceReferenceUMat;
        private Mat? colorDifferenceReferenceVMat;
        private Mat? contrastBlackReferenceYMat;
        private Mat? contrastWhiteReferenceYMat;

        public ConoscopeGlobalReferenceStore(ConoscopeConfig config)
        {
            this.config = config;
            LoadPersistedReferences();
        }

        public Mat? ColorDifferenceReferenceUMat => colorDifferenceReferenceUMat;
        public Mat? ColorDifferenceReferenceVMat => colorDifferenceReferenceVMat;
        public string? ColorDifferenceReferenceFileName { get; private set; }
        public string? ContrastBlackReferenceFileName { get; private set; }
        public string? ContrastWhiteReferenceFileName { get; private set; }

        public bool HasColorDifferenceReference => colorDifferenceReferenceUMat != null && colorDifferenceReferenceVMat != null;

        public bool HasContrastReference(ContrastReferenceKind referenceKind)
        {
            return GetContrastReferenceYMat(referenceKind) != null;
        }

        public Mat? GetContrastReferenceYMat(ContrastReferenceKind referenceKind)
        {
            return referenceKind == ContrastReferenceKind.Black ? contrastBlackReferenceYMat : contrastWhiteReferenceYMat;
        }

        public string? GetContrastReferenceFileName(ContrastReferenceKind referenceKind)
        {
            return referenceKind == ContrastReferenceKind.Black ? ContrastBlackReferenceFileName : ContrastWhiteReferenceFileName;
        }

        public void SaveColorDifferenceReference(Mat uMat, Mat vMat, string? fileName)
        {
            string uPath = EnsurePersistedFilePath(config.ColorDifferenceReferenceUMatPath, "color-difference-u.bin");
            string vPath = EnsurePersistedFilePath(config.ColorDifferenceReferenceVMatPath, "color-difference-v.bin");

            ConoscopeReferenceMatSerializer.Save(uPath, uMat);
            ConoscopeReferenceMatSerializer.Save(vPath, vMat);

            ReplaceMat(ref colorDifferenceReferenceUMat, uMat);
            ReplaceMat(ref colorDifferenceReferenceVMat, vMat);
            ColorDifferenceReferenceFileName = fileName;

            config.ColorDifferenceReferenceUMatPath = uPath;
            config.ColorDifferenceReferenceVMatPath = vPath;
            config.ColorDifferenceReferenceDisplayName = fileName ?? string.Empty;
            SaveConfig();
        }

        public void ClearColorDifferenceReference()
        {
            DeletePersistedFile(config.ColorDifferenceReferenceUMatPath);
            DeletePersistedFile(config.ColorDifferenceReferenceVMatPath);

            colorDifferenceReferenceUMat?.Dispose();
            colorDifferenceReferenceUMat = null;
            colorDifferenceReferenceVMat?.Dispose();
            colorDifferenceReferenceVMat = null;
            ColorDifferenceReferenceFileName = null;

            config.ColorDifferenceReferenceUMatPath = string.Empty;
            config.ColorDifferenceReferenceVMatPath = string.Empty;
            config.ColorDifferenceReferenceDisplayName = string.Empty;
            SaveConfig();
        }

        public void SaveContrastReference(ContrastReferenceKind referenceKind, Mat yMat, string? fileName)
        {
            string persistedPath = EnsurePersistedFilePath(
                referenceKind == ContrastReferenceKind.Black ? config.ContrastBlackReferenceYMatPath : config.ContrastWhiteReferenceYMatPath,
                referenceKind == ContrastReferenceKind.Black ? "contrast-black-y.bin" : "contrast-white-y.bin");

            ConoscopeReferenceMatSerializer.Save(persistedPath, yMat);

            if (referenceKind == ContrastReferenceKind.Black)
            {
                ReplaceMat(ref contrastBlackReferenceYMat, yMat);
                ContrastBlackReferenceFileName = fileName;
                config.ContrastBlackReferenceYMatPath = persistedPath;
                config.ContrastBlackReferenceDisplayName = fileName ?? string.Empty;
                SaveConfig();
                return;
            }

            ReplaceMat(ref contrastWhiteReferenceYMat, yMat);
            ContrastWhiteReferenceFileName = fileName;
            config.ContrastWhiteReferenceYMatPath = persistedPath;
            config.ContrastWhiteReferenceDisplayName = fileName ?? string.Empty;
            SaveConfig();
        }

        public void ClearContrastReference(ContrastReferenceKind referenceKind)
        {
            if (referenceKind == ContrastReferenceKind.Black)
            {
                DeletePersistedFile(config.ContrastBlackReferenceYMatPath);
                contrastBlackReferenceYMat?.Dispose();
                contrastBlackReferenceYMat = null;
                ContrastBlackReferenceFileName = null;
                config.ContrastBlackReferenceYMatPath = string.Empty;
                config.ContrastBlackReferenceDisplayName = string.Empty;
                SaveConfig();
                return;
            }

            DeletePersistedFile(config.ContrastWhiteReferenceYMatPath);
            contrastWhiteReferenceYMat?.Dispose();
            contrastWhiteReferenceYMat = null;
            ContrastWhiteReferenceFileName = null;
            config.ContrastWhiteReferenceYMatPath = string.Empty;
            config.ContrastWhiteReferenceDisplayName = string.Empty;
            SaveConfig();
        }

        public void Dispose()
        {
            colorDifferenceReferenceUMat?.Dispose();
            colorDifferenceReferenceUMat = null;
            colorDifferenceReferenceVMat?.Dispose();
            colorDifferenceReferenceVMat = null;
            contrastBlackReferenceYMat?.Dispose();
            contrastBlackReferenceYMat = null;
            contrastWhiteReferenceYMat?.Dispose();
            contrastWhiteReferenceYMat = null;
        }

        private void LoadPersistedReferences()
        {
            ColorDifferenceReferenceFileName = string.IsNullOrWhiteSpace(config.ColorDifferenceReferenceDisplayName)
                ? null
                : config.ColorDifferenceReferenceDisplayName;
            ContrastBlackReferenceFileName = string.IsNullOrWhiteSpace(config.ContrastBlackReferenceDisplayName)
                ? null
                : config.ContrastBlackReferenceDisplayName;
            ContrastWhiteReferenceFileName = string.IsNullOrWhiteSpace(config.ContrastWhiteReferenceDisplayName)
                ? null
                : config.ContrastWhiteReferenceDisplayName;

            TryLoadMat(config.ColorDifferenceReferenceUMatPath, ref colorDifferenceReferenceUMat, "色差 U");
            TryLoadMat(config.ColorDifferenceReferenceVMatPath, ref colorDifferenceReferenceVMat, "色差 V");
            TryLoadMat(config.ContrastBlackReferenceYMatPath, ref contrastBlackReferenceYMat, "黑场 Y");
            TryLoadMat(config.ContrastWhiteReferenceYMatPath, ref contrastWhiteReferenceYMat, "白场 Y");
        }

        private static string EnsurePersistedFilePath(string configuredPath, string defaultFileName)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ColorVision",
                "Conoscope",
                "References");
            return Path.Combine(root, defaultFileName);
        }

        private void TryLoadMat(string filePath, ref Mat? target, string label)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                using Mat loaded = ConoscopeReferenceMatSerializer.Load(filePath);
                ReplaceMat(ref target, loaded);
            }
            catch (Exception ex)
            {
                log.Warn($"加载 {label} 参考矩阵失败: {ex.Message}", ex);
            }
        }

        private static void ReplaceMat(ref Mat? target, Mat source)
        {
            target?.Dispose();
            target = source.Clone();
        }

        private static void DeletePersistedFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                log.Warn($"删除参考矩阵文件失败: {ex.Message}", ex);
            }
        }

        private static void SaveConfig()
        {
            ConfigService.Instance.Save<ConoscopeConfig>();
        }
    }
}
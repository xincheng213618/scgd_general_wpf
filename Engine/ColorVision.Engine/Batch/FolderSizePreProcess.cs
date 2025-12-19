using ColorVision.Common.MVVM;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Configuration for folder size monitoring and cleanup
    /// </summary>
    public class FolderSizePreProcessConfig : ViewModelBase
    {
        [DisplayName("监控文件夹")]
        [Description("要监控的文件夹路径")]
        [PropertyEditorType(typeof(ColorVision.UI.TextSelectFolderPropertiesEditor))]
        public string FolderPath { get => _FolderPath; set { _FolderPath = value; OnPropertyChanged(); } }
        private string _FolderPath = string.Empty;

        [DisplayName("大小限制(MB)")]
        [Description("文件夹大小限制，单位为MB。超过此大小将删除最旧的文件")]
        public long MaxSizeMB { get => _MaxSizeMB; set { _MaxSizeMB = value; OnPropertyChanged(); } }
        private long _MaxSizeMB = 1024; // Default 1GB

        [DisplayName("文件扩展名")]
        [Description("要监控的文件扩展名（逗号分隔，例如: .jpg,.png,.tiff）。留空表示监控所有文件")]
        public string FileExtensions { get => _FileExtensions; set { _FileExtensions = value; OnPropertyChanged(); } }
        private string _FileExtensions = ".jpg,.png,.tiff,.bmp";

        [DisplayName("包含子文件夹")]
        [Description("是否包含子文件夹中的文件")]
        public bool IncludeSubfolders { get => _IncludeSubfolders; set { _IncludeSubfolders = value; OnPropertyChanged(); } }
        private bool _IncludeSubfolders = false;

        [DisplayName("启用")]
        [Description("是否启用此预处理")]
        public bool Enabled { get => _Enabled; set { _Enabled = value; OnPropertyChanged(); } }
        private bool _Enabled = true;
    }

    [PreProcess("文件夹大小检测", "检测文件夹大小，超出限制后删除最早的文件")]
    public class FolderSizePreProcess : PreProcessBase<FolderSizePreProcessConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FolderSizePreProcess));

        public override bool PreProcess(IPreProcessContext ctx)
        {
            if (!Config.Enabled)
            {
                log.Info("FolderSizePreProcess 未启用，跳过");
                return true;
            }

            if (string.IsNullOrWhiteSpace(Config.FolderPath))
            {
                log.Warn("FolderSizePreProcess: 文件夹路径未配置");
                return true; // Don't block flow execution
            }

            if (!Directory.Exists(Config.FolderPath))
            {
                log.Warn($"FolderSizePreProcess: 文件夹不存在 {Config.FolderPath}");
                return true; // Don't block flow execution
            }

            try
            {
                // Parse file extensions
                var extensions = ParseExtensions(Config.FileExtensions);
                
                // Get all files matching the criteria
                var searchOption = Config.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var allFiles = Directory.GetFiles(Config.FolderPath, "*.*", searchOption);
                
                // Filter by extensions if specified
                var files = extensions.Count > 0
                    ? allFiles.Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList()
                    : allFiles.ToList();

                if (files.Count == 0)
                {
                    log.Info($"FolderSizePreProcess: 文件夹中没有匹配的文件 {Config.FolderPath}");
                    return true;
                }

                // Calculate total size using File.GetLength() for better performance
                long totalSize = 0;
                var fileSizes = new Dictionary<string, long>();
                
                foreach (var file in files)
                {
                    try
                    {
                        long fileSize = new FileInfo(file).Length;
                        totalSize += fileSize;
                        fileSizes[file] = fileSize;
                    }
                    catch
                    {
                        // Skip files we can't access
                    }
                }

                if (fileSizes.Count == 0)
                {
                    log.Info($"FolderSizePreProcess: 没有可访问的文件 {Config.FolderPath}");
                    return true;
                }

                long totalSizeMB = totalSize / (1024 * 1024);
                long maxSizeBytes = Config.MaxSizeMB * 1024 * 1024;

                log.Info($"FolderSizePreProcess: 文件夹 {Config.FolderPath} 当前大小 {totalSizeMB}MB, 限制 {Config.MaxSizeMB}MB");

                if (totalSize <= maxSizeBytes)
                {
                    log.Info("文件夹大小在限制范围内，无需清理");
                    return true;
                }

                // Need to cleanup - create FileInfo only for files we're going to process
                // Sort by last write time to delete oldest first
                var filesByDate = fileSizes.Keys
                    .Select(f => new { Path = f, LastWriteTime = File.GetLastWriteTime(f), Size = fileSizes[f] })
                    .OrderBy(f => f.LastWriteTime)
                    .ToList();

                int deletedCount = 0;
                long deletedSize = 0;

                foreach (var file in filesByDate)
                {
                    if (totalSize - deletedSize <= maxSizeBytes)
                        break;

                    try
                    {
                        File.Delete(file.Path);
                        deletedSize += file.Size;
                        deletedCount++;
                        log.Info($"删除文件: {Path.GetFileName(file.Path)} ({file.Size / 1024}KB)");
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"删除文件失败: {Path.GetFileName(file.Path)}", ex);
                    }
                }

                long finalSize = (totalSize - deletedSize) / (1024 * 1024);
                log.Info($"FolderSizePreProcess: 清理完成。删除了 {deletedCount} 个文件，释放了 {deletedSize / (1024 * 1024)}MB。当前大小: {finalSize}MB");

                return true;
            }
            catch (Exception ex)
            {
                log.Error("FolderSizePreProcess 执行失败", ex);
                return true; // Don't block flow execution on errors
            }
        }

        private HashSet<string> ParseExtensions(string extensionsStr)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            if (string.IsNullOrWhiteSpace(extensionsStr))
                return result;

            var parts = extensionsStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var ext = part.Trim();
                if (!ext.StartsWith("."))
                    ext = "." + ext;
                result.Add(ext.ToLowerInvariant());
            }

            return result;
        }
    }
}

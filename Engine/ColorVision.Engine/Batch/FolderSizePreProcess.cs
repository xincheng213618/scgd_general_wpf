using log4net;
using Newtonsoft.Json;
using Quartz.Util;
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
    public class FolderSizePreProcessConfig : PreProcessConfigBase
    {

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        [DisplayName("监控文件夹")]
        [Description("要监控的文件夹路径")]
        [PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public  List<string> FolderPaths { get => _FolderPath; set { _FolderPath = value; OnPropertyChanged(); } }
        private List<string> _FolderPath = new List<string>() { "D:\\CVTest\\DEV.Camera.Default"};

        [DisplayName("大小限制(MB)")]
        [Description("文件夹大小限制，单位为MB。超过此大小将删除最旧的文件")]
        public long MaxSizeMB { get => _MaxSizeMB; set { _MaxSizeMB = value; OnPropertyChanged(); } }
        private long _MaxSizeMB = 102400; // Default 1GB

        [DisplayName("文件扩展名")]
        [Description("要监控的文件扩展名（逗号分隔，例如: .jpg,.png,.tiff）。留空表示监控所有文件")]
        public string FileExtensions { get => _FileExtensions; set { _FileExtensions = value; OnPropertyChanged(); } }
        private string _FileExtensions = ".jpg,.png,.tiff,.bmp,.cvraw,.cvcie";

        [DisplayName("包含子文件夹")]
        [Description("是否包含子文件夹中的文件")]
        public bool IncludeSubfolders { get => _IncludeSubfolders; set { _IncludeSubfolders = value; OnPropertyChanged(); } }
        private bool _IncludeSubfolders = true;
    }

    [PreProcess("文件夹大小检测", "检测文件夹大小，超出限制后删除最早的文件")]
    public class FolderSizePreProcess : PreProcessBase<FolderSizePreProcessConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FolderSizePreProcess));

        public override bool PreProcess(IPreProcessContext ctx)
        {
            foreach (var FolderPath in Config.FolderPaths)
            {
                if (string.IsNullOrWhiteSpace(FolderPath))
                {
                    log.Warn("FolderSizePreProcess: 文件夹路径未配置");
                    continue;
                }

                if (!Directory.Exists(FolderPath))
                {
                    log.Warn($"FolderSizePreProcess: 文件夹不存在 {Config.FolderPaths}");
                    continue;
                }

                try
                {
                    // Parse file extensions
                    var extensions = ParseExtensions(Config.FileExtensions);

                    // Get all files matching the criteria
                    var searchOption = Config.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var allFiles = Directory.GetFiles(FolderPath, "*.*", searchOption);

                    // Filter by extensions if specified
                    var files = extensions.Count > 0
                        ? allFiles.Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList()
                        : allFiles.ToList();

                    if (files.Count == 0)
                    {
                        log.Info($"FolderSizePreProcess: 文件夹中没有匹配的文件 {FolderPath}");
                        continue;
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
                        continue;
                    }
                    long totalSizeMB = totalSize / (1024 * 1024);
                    long maxSizeBytes = Config.MaxSizeMB * 1024 * 1024;

                    log.Info($"FolderSizePreProcess: 文件夹 {Config.FolderPaths} 当前大小 {totalSizeMB}MB, 限制 {Config.MaxSizeMB}MB");
                    if (totalSize <= maxSizeBytes)
                    {
                        continue;
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
                }
                catch (Exception ex)
                {
                    log.Error("FolderSizePreProcess 执行失败", ex);
                    return true; // Don't block flow execution on errors
                }
            }


            return true;
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

using log4net;
using Newtonsoft.Json;
using ColorVision.Engine.PropertyEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Batch.PreProcess
{
    /// <summary>
    /// Configuration for cache size monitoring and cleanup.
    /// </summary>
    public class FolderSizePreProcessConfig : PreProcessConfigBase
    {
        private const long OneGb = 1024L * 1024L * 1024L;
        private const long DefaultTriggerBytes = 100L * OneGb;
        private const long DefaultTargetBytes = 50L * OneGb;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        [Display(Name = "PreProcess_CacheFolders", Description = "PreProcess_CacheFoldersDesc", GroupName = "PreProcess_CacheCleanupGroup", ResourceType = typeof(Properties.Resources))]
        public List<string> FolderPaths { get => _FolderPath; set { _FolderPath = value; OnPropertyChanged(); } }
        private List<string> _FolderPath = new List<string>() { "D:\\CVTest\\DEV.Camera.Default" };

        [Display(Name = "PreProcess_CacheLimit", Description = "PreProcess_CacheLimitDesc", GroupName = "PreProcess_CacheCleanupGroup", ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(FolderSizeBytesPropertiesEditor))]
        public long TriggerSizeBytes { get => _TriggerSizeBytes; set { _TriggerSizeBytes = value; OnPropertyChanged(); } }
        private long _TriggerSizeBytes = DefaultTriggerBytes;

        [Display(Name = "PreProcess_CleanupTarget", Description = "PreProcess_CleanupTargetDesc", GroupName = "PreProcess_CacheCleanupGroup", ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(FolderSizeBytesPropertiesEditor))]
        public long TargetSizeBytes { get => _TargetSizeBytes; set { _TargetSizeBytes = value; OnPropertyChanged(); } }
        private long _TargetSizeBytes = DefaultTargetBytes;

        [Display(Name = "PreProcess_CacheFileTypes", Description = "PreProcess_CacheFileTypesDesc", GroupName = "PreProcess_CacheCleanupGroup", ResourceType = typeof(Properties.Resources))]
        public string FileExtensions { get => _FileExtensions; set { _FileExtensions = value; OnPropertyChanged(); } }
        private string _FileExtensions = ".jpg,.png,.tiff,.bmp,.cvraw,.cvcie";

        [Display(Name = "PreProcess_IncludeSubfolders", Description = "PreProcess_IncludeSubfoldersDesc", GroupName = "PreProcess_CacheCleanupGroup", ResourceType = typeof(Properties.Resources))]
        public bool IncludeSubfolders { get => _IncludeSubfolders; set { _IncludeSubfolders = value; OnPropertyChanged(); } }
        private bool _IncludeSubfolders = true;
    }

    [PreProcess("PreProcess_CacheCleanupName", "PreProcess_CacheCleanupDesc", ResourceType = typeof(Properties.Resources))]
    public class FolderSizePreProcess : PreProcessBase<FolderSizePreProcessConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FolderSizePreProcess));
        private static readonly char[] ExtensionSeparators = { ',', ';', ' ' };
        private const long OneMb = 1024L * 1024L;

        public override Task<bool> PreProcess(IPreProcessContext ctx)
        {
            var (triggerBytes, targetBytes) = NormalizeThresholds();

            if (triggerBytes <= 0)
            {
                log.Warn("FolderSizePreProcess: 触发上限必须大于 0");
                return Task.FromResult(true);
            }

            foreach (var cachePath in Config.FolderPaths)
            {
                if (string.IsNullOrWhiteSpace(cachePath))
                {
                    log.Warn("CacheCleanupPreProcess: 缓存目录未配置");
                    continue;
                }

                if (!Directory.Exists(cachePath))
                {
                    log.Warn($"CacheCleanupPreProcess: 缓存目录不存在 {cachePath}");
                    continue;
                }

                try
                {
                    // Parse file extensions
                    var extensions = ParseExtensions(Config.FileExtensions);

                    // Get all files matching the criteria
                    var searchOption = Config.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    var allFiles = Directory.GetFiles(cachePath, "*.*", searchOption);

                    // Filter by extensions if specified
                    var files = extensions.Count > 0
                        ? allFiles.Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList()
                        : allFiles.ToList();

                    if (files.Count == 0)
                    {
                        log.Info($"CacheCleanupPreProcess: 缓存目录中没有匹配的文件 {cachePath}");
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
                    long triggerMB = triggerBytes / OneMb;
                    long targetMB = targetBytes / OneMb;

                    log.Info($"CacheCleanupPreProcess: 缓存目录 {cachePath} 当前大小 {totalSizeMB}MB, 缓存上限 {triggerMB}MB, 清理到 {targetMB}MB");
                    if (totalSize <= triggerBytes)
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
                        if (totalSize - deletedSize <= targetBytes)
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
                    log.Info($"CacheCleanupPreProcess: 清理完成。删除了 {deletedCount} 个缓存文件，释放了 {deletedSize / (1024 * 1024)}MB。当前大小: {finalSize}MB");
                }
                catch (Exception ex)
                {
                    log.Error("CacheCleanupPreProcess 执行失败", ex);
                    return Task.FromResult(false);

                }
            }
            return Task.FromResult(true);
        }

        private (long TriggerBytes, long TargetBytes) NormalizeThresholds()
        {
            long triggerBytes = Config.TriggerSizeBytes;
            long targetBytes = Config.TargetSizeBytes;

            if (triggerBytes <= 0)
            {
                return (0, 0);
            }

            if (targetBytes <= 0)
            {
                targetBytes = Math.Max(OneMb, triggerBytes / 2);
            }

            if (targetBytes >= triggerBytes)
            {
                targetBytes = Math.Max(OneMb, (long)(triggerBytes * 0.8));
            }

            if (Config.TargetSizeBytes != targetBytes)
            {
                Config.TargetSizeBytes = targetBytes;
            }

            return (triggerBytes, targetBytes);
        }

        private static HashSet<string> ParseExtensions(string extensionsStr)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            if (string.IsNullOrWhiteSpace(extensionsStr))
                return result;

            var parts = extensionsStr.Split(ExtensionSeparators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var ext = part.Trim();
                if (!ext.StartsWith('.'))
                    ext = "." + ext;
                result.Add(ext.ToLowerInvariant());
            }

            return result;
        }
    }
}

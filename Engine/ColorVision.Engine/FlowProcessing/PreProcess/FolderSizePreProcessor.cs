using log4net;
using Newtonsoft.Json;
using ColorVision.Engine.PropertyEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.PreProcess
{
    /// <summary>
    /// Configuration for cache size monitoring and cleanup.
    /// </summary>
    public class FolderSizePreProcessorConfig : PreProcessConfigBase
    {
        private const long OneGb = 1024L * 1024L * 1024L;
        private const long DefaultTriggerBytes = 100L * OneGb;
        private const long DefaultTargetBytes = 50L * OneGb;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        [Display(Name = "PreProcess_CacheFolders", Description = "PreProcess_CacheFoldersDesc", GroupName = "PreProcess_CacheCleanupGroup", ResourceType = typeof(Properties.Resources))]
        [CollectionEditorType(typeof(TextSelectFolderPropertiesEditor))]
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
    public class FolderSizePreProcessor : PreProcessorBase<FolderSizePreProcessorConfig>
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FolderSizePreProcessor));
        private static readonly char[] ExtensionSeparators = { ',', ';', ' ' };
        private const long OneMb = 1024L * 1024L;
        private readonly SemaphoreSlim _executionGate = new(1, 1);
        private readonly Func<Func<bool>, Task<bool>> _runInBackground;

        public FolderSizePreProcessor() : this(work => Task.Run(work))
        {
        }

        internal FolderSizePreProcessor(Func<Func<bool>, Task<bool>> runInBackground)
        {
            _runInBackground = runInBackground ?? throw new ArgumentNullException(nameof(runInBackground));
        }

        public override async Task<bool> PreProcess(PreProcessContext ctx)
        {
            var (triggerBytes, targetBytes) = NormalizeThresholds();

            if (triggerBytes <= 0)
            {
                log.Warn("FolderSizePreProcessor: 触发上限必须大于 0");
                return true;
            }

            string[] folderPaths = Config.FolderPaths?.ToArray() ?? Array.Empty<string>();
            string fileExtensions = Config.FileExtensions;
            bool includeSubfolders = Config.IncludeSubfolders;

            await _executionGate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await _runInBackground(() => ExecuteCleanup(folderPaths, fileExtensions, includeSubfolders, triggerBytes, targetBytes)).ConfigureAwait(false);
            }
            finally
            {
                _executionGate.Release();
            }
        }

        private static bool ExecuteCleanup(string[] folderPaths, string fileExtensions, bool includeSubfolders, long triggerBytes, long targetBytes)
        {
            foreach (var cachePath in folderPaths)
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
                    var scanTiming = System.Diagnostics.Stopwatch.StartNew();
                    // Parse file extensions
                    var extensions = ParseExtensions(fileExtensions);

                    var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    long totalSize = 0;
                    int scannedFiles = 0;
                    int matchedFiles = 0;
                    int unreadableFiles = 0;
                    var files = new List<(FileInfo Info, long Size)>();

                    // Directory enumeration pre-populates size and timestamps; reuse them instead of querying each path again.
                    foreach (var file in new DirectoryInfo(cachePath).EnumerateFiles("*.*", searchOption))
                    {
                        scannedFiles++;
                        if (extensions.Count > 0 && !extensions.Contains(file.Extension)) continue;
                        matchedFiles++;
                        try
                        {
                            long fileSize = file.Length;
                            totalSize += fileSize;
                            files.Add((file, fileSize));
                        }
                        catch
                        {
                            unreadableFiles++;
                            // Skip files we can't access
                        }
                    }
                    if (matchedFiles == 0)
                    {
                        log.Debug($"CacheCleanupPreProcess: 缓存目录中没有匹配的文件 {cachePath}");
                        continue;
                    }
                    if (unreadableFiles > 0)
                        log.Warn($"CacheCleanupPreProcess: 缓存扫描跳过了 {unreadableFiles} 个无法读取大小的文件，目录 {cachePath}");
                    if (files.Count == 0)
                    {
                        continue;
                    }
                    long totalSizeMB = totalSize / (1024 * 1024);
                    long triggerMB = triggerBytes / OneMb;
                    long targetMB = targetBytes / OneMb;

                    scanTiming.Stop();
                    log.Debug($"CacheCleanupPreProcess: 缓存目录 {cachePath} 当前大小 {totalSizeMB}MB, 缓存上限 {triggerMB}MB, 清理到 {targetMB}MB");
                    if (totalSize <= triggerBytes)
                    {
                        continue;
                    }

                    var filesByDate = files.OrderBy(f => f.Info.LastWriteTime);

                    int deletedCount = 0;
                    long deletedSize = 0;
                    int deleteFailures = 0;
                    DateTime? firstDeletedWriteTime = null;
                    DateTime? lastDeletedWriteTime = null;
                    var deleteTiming = System.Diagnostics.Stopwatch.StartNew();

                    foreach (var file in filesByDate)
                    {
                        if (totalSize - deletedSize <= targetBytes)
                            break;

                        try
                        {
                            File.Delete(file.Info.FullName);
                            deletedSize += file.Size;
                            deletedCount++;
                            firstDeletedWriteTime ??= file.Info.LastWriteTime;
                            lastDeletedWriteTime = file.Info.LastWriteTime;
                            if (log.IsDebugEnabled) log.Debug($"删除文件: {file.Info.Name} ({file.Size / 1024}KB)");
                        }
                        catch (Exception ex)
                        {
                            deleteFailures++;
                            log.Warn($"删除文件失败: {file.Info.Name}", ex);
                        }
                    }

                    long finalSize = (totalSize - deletedSize) / (1024 * 1024);
                    log.InfoFormat("CacheCleanupPreProcess: 清理完成。Directory={0} FilesScanned={1} UnreadableFiles={2} DeletedFiles={3} DeleteFailures={4} ReleasedMiB={5} RemainingMiB={6} ScanMs={7:F3} DeleteMs={8:F3} BeforeMiB={9} TriggerMiB={10} TargetMiB={11} FirstDeletedWriteTime={12:O} LastDeletedWriteTime={13:O}",
                        cachePath, scannedFiles, unreadableFiles, deletedCount, deleteFailures,
                        deletedSize / OneMb, finalSize, scanTiming.Elapsed.TotalMilliseconds, deleteTiming.Elapsed.TotalMilliseconds,
                        totalSizeMB, triggerMB, targetMB, firstDeletedWriteTime, lastDeletedWriteTime);
                }
                catch (Exception ex)
                {
                    log.Error("CacheCleanupPreProcess 执行失败", ex);
                    return false;

                }
            }
            return true;
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

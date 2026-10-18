using log4net;
using ColorVision.Engine.Services.Files;
using Newtonsoft.Json;
using ColorVision.Engine.PropertyEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
            try
            {
                FolderCleanupService.Execute(folderPaths, fileExtensions, includeSubfolders, triggerBytes, targetBytes);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("CacheCleanupPreProcess 执行失败", ex);
                return false;
            }
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

    }
}

using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using System.Collections.Generic;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    public sealed class FlowProcessingConfigurationFeedbackCollector : IFeedbackLogCollector
    {
        public string Name => "流程前后处理配置";
        public string Description => "当前前处理、后处理配置，包含缓存清理等操作；敏感字段已脱敏";
        public int Order => 16;
        public bool IsSelectedByDefault => true;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles() => FeedbackConfigurationSnapshot.CollectFiles(
        [
            ("Config/PreProcessConfig.json", PreProcessManager.PersistFilePath),
            ("Config/PostProcessConfig.json", PostProcessManager.PersistFilePath),
        ]);
    }
}

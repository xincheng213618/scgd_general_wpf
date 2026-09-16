using ColorVision.UI;
using ColorVision.UI.LogImp;
using ProjectARVRPro.Process;

namespace ProjectARVRPro
{
    public sealed class ProjectARVRProConfigurationFeedbackCollector : IFeedbackLogCollector
    {
        public string Name => "ARVRPro 流程配置";
        public string Description => "当前流程组、切图等待、相机覆盖参数及 Recipe；敏感字段已脱敏";
        public int Order => 17;
        public bool IsSelectedByDefault => true;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles() => FeedbackConfigurationSnapshot.CollectFiles(
            [("Config/ProjectARVRProProcessGroups.json", ProcessManager.GroupPersistFilePath)]);
    }
}

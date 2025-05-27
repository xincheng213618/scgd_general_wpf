using ColorVision.Common.Utilities;

namespace ColorVision.Solution.Editor
{
    // 标记为通用编辑器
    [GenericEditor("系统默认")]
    public class SystemEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            PlatformHelper.Open(filePath);
        }
    }
}
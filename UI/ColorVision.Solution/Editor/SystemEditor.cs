using ColorVision.Common.Utilities;

namespace ColorVision.Solution.Editor
{
    // 标记为通用编辑器
    [GenericEditor("系统默认", resourceKey: "Sol_Editor_System", editorId: "colorvision.system", isDefault: true, priority: -100)]
    public class SystemEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            PlatformHelper.Open(filePath);
        }
    }
}

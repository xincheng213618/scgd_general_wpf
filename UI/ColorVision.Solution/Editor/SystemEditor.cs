using ColorVision.Common.Utilities;
using System.Windows.Controls;

namespace ColorVision.Solution.Editor
{
    // 标记为通用编辑器
    [GenericEditor("系统默认")]
    public class SystemEditor : EditorBase
    {
        public override Control? Open(string filePath)
        {
            PlatformHelper.Open(filePath);
            return null;
        }
    }
}
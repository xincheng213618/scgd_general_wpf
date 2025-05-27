using ColorVision.Common.Utilities;
using System.Windows.Controls;

namespace ColorVision.Solution.Editor
{
    // 标记为通用编辑器
    [GenericEditor]
    public class SystemEditor : EditorBase
    {
        public override string Name => "系统默认打开";

        public override Control? Open(string filePath)
        {
            PlatformHelper.Open(filePath);
            return null;
        }
    }
}

using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;

namespace WindowsServicePlugin
{
    public abstract class ExportLogBase : MenuItemBase
    {
        public abstract string Url { get; }

        public override void Execute()
        {
            PlatformHelper.OpenFolder(Url);
        }
    }
}

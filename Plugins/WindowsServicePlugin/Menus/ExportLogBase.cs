
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;

namespace WindowsServicePlugin.Menus
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

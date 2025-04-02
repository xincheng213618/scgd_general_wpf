
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using log4net;

namespace WindowsServicePlugin.Menus
{
    public abstract class ExportLogBase : MenuItemBase
    {
        private static ILog log = LogManager.GetLogger(typeof(ExportLogBase));
        public abstract string Url { get; }

        public override void Execute()
        {
            log.Info($"Open:{Url}");
            PlatformHelper.OpenFolder(Url);
        }
    }
}

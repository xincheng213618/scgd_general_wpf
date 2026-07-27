using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;

namespace ColorVision.Update
{
    public class MenuUpdatelocal : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => ColorVision.Properties.Resources.DownloadLocal;
        public override int Order => 10;

        public override void Execute()
        {
            PlatformHelper.Open("http://xc213618.ddns.me:9998/");
        }
    }
}

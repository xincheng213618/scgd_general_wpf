using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;

namespace ColorVision.Update.Export
{
    public class MenuUpdatelocal : MenuItemBase
    {
        public override string OwnerGuid => "Update";
        public override string GuidId => "Updatelocal";
        public override string Header => "本地下载";
        public override int Order => 10;

        public override void Execute()
        {
            PlatformHelper.Open("http://xc213618.ddns.me:9998/upload/ColorVision/");
        }
    }
}

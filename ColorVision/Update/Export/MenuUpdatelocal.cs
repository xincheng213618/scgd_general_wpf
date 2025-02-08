using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;

namespace ColorVision.Update
{
    public class MenuUpdatelocal : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => "本地下载";
        public override int Order => 10;

        public override void Execute()
        {
            PlatformHelper.Open("http://xc213618.ddns.me:9998/upload/ColorVision/");
        }
    }


    public class MenuUpdateBaidu : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);
        public override string Header => "百度云盘下载";
        public override int Order => 11;

        public override void Execute()
        {
            PlatformHelper.Open("https://pan.baidu.com/s/166pUmh2az_oTcihykXj1jQ?pwd=3618");
        }
    }
}

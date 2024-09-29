using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;

namespace ColorVision.Update.Export
{
    public class MenuUpdateBaidu : MenuItemBase
    {
        public override string OwnerGuid => "Update";
        public override string GuidId => "UpdateBaidu";
        public override string Header => "百度云下载";
        public override int Order => 10;

        public override void Execute()
        {
            PlatformHelper.Open("https://pan.baidu.com/s/1cB4IP4F2NppYmRl8fQantw?pwd=tz67");
        }
    }
}

using ColorVision.Common.Utilities;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;

namespace ColorVision.Update.Export
{
    public class MenuUpload : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "Upload";
        public override int Order => 1000;
        public override string Header => "上传文件";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            PlatformHelper.Open("http://xc213618.ddns.me:9998");
        }
    }
}

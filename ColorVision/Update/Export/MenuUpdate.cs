using ColorVision.Properties;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;

namespace ColorVision.Update
{
    [RequiresPermission(PermissionMode.Administrator)]
    public class MenuUpdate : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override string Header => Resources.Update;
        public override int Order => 10001;
    }
}

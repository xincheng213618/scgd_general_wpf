using ColorVision.UI.Properties;

namespace ColorVision.UI.Menus.Base
{

    public class MenuEdit : GlobalMenuBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;
        public override string GuidId => MenuItemConstants.Edit;
        public override string Header => Resources.MenuEdit;
        public override int Order => 2;
    }
    public abstract class MenuItemEditBase : MenuItemBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;
        public override string OwnerGuid => MenuItemConstants.Edit;
    }
}

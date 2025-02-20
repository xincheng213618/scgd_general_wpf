using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;

namespace ColorVision.Engine.Services.Devices.Camera.Templates
{
    public class MenuItemCamera : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 10;
        public override string Header => "相机";

    }

}

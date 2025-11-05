using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus
{
    public class MenuAutoFocus : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuItemCamera);
        public override string Header => ColorVision.Engine.Properties.Resources.AutoFocusTemplate;
        public override int Order => 23;
        public override ITemplate Template => new TemplateAutoFocus();
    }
}

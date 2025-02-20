using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus
{
    public class MenuAutoFocus : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuItemCamera);
        public override string Header => "自动聚焦模板";
        public override int Order => 23;
        public override ITemplate Template => new TemplateAutoFocus();
    }
}

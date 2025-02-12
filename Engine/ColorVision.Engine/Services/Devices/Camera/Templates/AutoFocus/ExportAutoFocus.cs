using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus
{
    public class ExportAutoFocus : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string GuidId => nameof(ExportAutoFocus);
        public override string Header => "自动聚焦模板设置";
        public override int Order => 23;
        public override ITemplate Template => new TemplateAutoFocus();
    }
}

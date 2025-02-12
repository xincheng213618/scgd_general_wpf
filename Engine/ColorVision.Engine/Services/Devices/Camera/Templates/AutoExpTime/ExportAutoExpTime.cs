using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam
{
    public class ExportAutoExpTime : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string GuidId => "AutoExpTime";
        public override string Header => Properties.Resources.AutoExploreTemplate;
        public override int Order => 23;
        public override ITemplate Template => new TemplateAutoExpTime();
    }
}

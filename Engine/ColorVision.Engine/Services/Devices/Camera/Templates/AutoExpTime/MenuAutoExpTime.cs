using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam
{
    public class MenuAutoExpTime : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuItemCamera);
        public override string Header => Properties.Resources.AutoExploreTemplate;
        public override int Order => 23;
        public override ITemplate Template => new TemplateAutoExpTime();
    }
}

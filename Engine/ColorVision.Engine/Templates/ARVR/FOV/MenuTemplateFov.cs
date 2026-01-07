using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.FOV
{
    public class MenuTemplateFov : MenuITemplateAlgorithmBase
    {
        public override string Header => "FOV1.0";
        public override int Order => 1005;
        public override ITemplate Template => new TemplateFOV();
    }
}

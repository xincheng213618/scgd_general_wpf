namespace ColorVision.Engine.Templates.FOV
{
    public class MenuTemplateFov : MenuITemplateAlgorithmBase
    {
        public override string Header => Properties.Resources.MenuFOV;
        public override int Order => 5;
        public override ITemplate Template => new TemplateFOV();
    }
}

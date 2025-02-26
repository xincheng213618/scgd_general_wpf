namespace ColorVision.Engine.Templates.Distortion
{
    public class ExportDistortionParam : MenuITemplateAlgorithmBase
    {
        public override string Header => Properties.Resources.MenuDistortion;
        public override int Order => 3;
        public override ITemplate Template => new TemplateDistortionParam();
    }
}

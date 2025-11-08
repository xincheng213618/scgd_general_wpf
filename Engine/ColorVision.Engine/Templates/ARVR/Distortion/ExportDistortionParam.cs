using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Distortion
{
    public class ExportDistortionParam : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.DistortionEvaluation;
        public override int Order => 1003;
        public override ITemplate Template => new TemplateDistortionParam();
    }
}

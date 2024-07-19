using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Distortion
{
    public class ExportDistortionParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "DistortionParam";
        public override string Header => Properties.Resources.MenuDistortion;
        public override int Order => 3;
        public override ITemplate Template => new TemplateDistortionParam();
    }
}

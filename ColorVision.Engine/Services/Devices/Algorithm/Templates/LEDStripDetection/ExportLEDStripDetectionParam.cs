using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LEDStripDetection
{
    public class ExportLEDStripDetectionParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "LEDStripDetection";
        public override string Header => "灯带检测模板设置";
        public override int Order => 2;
        public override ITemplate Template => new TemplateLEDStripDetectionParam();
    }
}

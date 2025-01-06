namespace ColorVision.Engine.Templates.LEDStripDetection
{
    public class ExportLEDStripDetectionParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "LEDStripDetection";
        public override string Header => "灯带检测";
        public override int Order => 2;
        public override ITemplate Template => new TemplateLEDStripDetection();
    }
}

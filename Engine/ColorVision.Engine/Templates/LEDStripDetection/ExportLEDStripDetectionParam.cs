namespace ColorVision.Engine.Templates.LEDStripDetection
{
    public class ExportLEDStripDetectionParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "灯带检测";
        public override int Order => 41326;
        public override ITemplate Template => new TemplateLEDStripDetection();
    }
}

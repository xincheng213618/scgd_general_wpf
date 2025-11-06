using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.LEDStripDetection
{
    public class ExportLEDStripDetectionParam : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.LightBandDetection;
        public override int Order => 41326;
        public override ITemplate Template => new TemplateLEDStripDetection();
    }
}

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.LedCheck
{
    public class ExportLedCheckParam : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.PixelLedDetect;
        public override int Order => 41324;
        public override ITemplate Template => new TemplateLedCheck();
    }
}

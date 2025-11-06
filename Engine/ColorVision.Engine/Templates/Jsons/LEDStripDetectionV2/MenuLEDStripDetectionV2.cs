using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{
    public class MenuLEDStripDetectionV2 : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.LightBarDetectionV2;
        public override int Order => 1003;
        public override ITemplate Template => new TemplateLEDStripDetectionV2();
    }

}

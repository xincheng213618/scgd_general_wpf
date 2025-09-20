using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.LEDStripDetectionV2
{
    public class MenuLEDStripDetectionV2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "灯条检测V2";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateLEDStripDetectionV2();
    }

}

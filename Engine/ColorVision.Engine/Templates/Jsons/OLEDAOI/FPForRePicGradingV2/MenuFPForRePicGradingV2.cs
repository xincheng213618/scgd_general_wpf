using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForRePicGradingV2
{
    public class MenuFPForRePicGradingV2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "缺陷检测V2";
        public override int Order => 1056;
        public override ITemplate Template => new TemplateFPForRePicGradingV2();
    }
}

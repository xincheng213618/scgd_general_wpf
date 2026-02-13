using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForBlackScreen
{
    public class MenuFPForBlackScreen : MenuITemplateAlgorithmBase
    {
        public override string Header => "黑画面检测";
        public override int Order => 1057;
        public override ITemplate Template => new TemplateFPForBlackScreen();
    }
}

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI.FPForQuardImg
{
    public class MenuFPForQuardImg : MenuITemplateAlgorithmBase
    {
        public override string Header => "亮点检测";
        public override int Order => 1055;
        public override ITemplate Template => new TemplateFPForQuardImg();
    }
}

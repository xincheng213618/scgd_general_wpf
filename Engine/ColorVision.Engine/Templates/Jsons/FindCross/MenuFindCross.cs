using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.FindCross
{
    public class MenuFindCross : MenuITemplateAlgorithmBase
    {
        public override string Header => "十字计算";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateFindCross();
    }

}

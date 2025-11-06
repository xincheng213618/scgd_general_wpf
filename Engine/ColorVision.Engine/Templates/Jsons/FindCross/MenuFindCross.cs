using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.FindCross
{
    public class MenuFindCross : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.CrossCalculation;
        public override int Order => 1003;
        public override ITemplate Template => new TemplateFindCross();
    }

}

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{
    public class ExportBlackMura : MenuITemplateAlgorithmBase
    {
        public override string Header => "BlackMura";
        public override int Order => 2;
        public override ITemplate Template => new TemplateBlackMura();
    }
}

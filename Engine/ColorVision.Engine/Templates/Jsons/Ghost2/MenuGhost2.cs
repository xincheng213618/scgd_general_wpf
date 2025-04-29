

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{
    public class MenuGhost2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "鬼影2";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateGhostQK();
    }

}



namespace ColorVision.Engine.Templates.Jsons.GhostQK
{
    public class MenuGhostQK : MenuITemplateAlgorithmBase
    {
        public override string Header => "鬼影QK";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateGhostQK();
    }

}

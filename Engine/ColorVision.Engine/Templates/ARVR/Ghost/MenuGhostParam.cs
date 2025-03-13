namespace ColorVision.Engine.Templates.Ghost
{
    public class MenuGhostParam : MenuITemplateAlgorithmBase
    {
        public override string Header => "鬼影";
        public override int Order => 1003;
        public override ITemplate Template => new TemplateGhost();
    }


}

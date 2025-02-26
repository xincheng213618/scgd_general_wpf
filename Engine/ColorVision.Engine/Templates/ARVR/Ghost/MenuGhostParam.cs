namespace ColorVision.Engine.Templates.Ghost
{
    public class MenuGhostParam : MenuITemplateAlgorithmBase
    {
        public override string Header => Properties.Resources.MenuGhost;
        public override int Order => 3;
        public override ITemplate Template => new TemplateGhost();
    }


}

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Ghost
{
    public class MenuGhostParam : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.GhostShadow;
        public override int Order => 1003;
        public override ITemplate Template => new TemplateGhost();
    }


}

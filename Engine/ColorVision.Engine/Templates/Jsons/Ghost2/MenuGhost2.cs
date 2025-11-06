

using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.Ghost2
{
    public class MenuGhost2 : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.GhostingDetection2_0;
        public override int Order => 1003;
        public override ITemplate Template => new TemplateGhostQK();
    }

}

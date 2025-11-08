
using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Matching
{
    public class ExportMenuItemMatching : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.TemplateMatching;
        public override int Order => 50;
        public override ITemplate Template => new TemplateMatch();
    }
}

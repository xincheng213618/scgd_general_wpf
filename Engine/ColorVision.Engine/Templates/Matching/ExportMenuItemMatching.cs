
using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Matching
{
    public class ExportMenuItemMatching : MenuITemplateAlgorithmBase
    {
        public override string Header => "模板匹配";
        public override int Order => 50;
        public override ITemplate Template => new TemplateMatch();
    }
}

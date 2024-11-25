
namespace ColorVision.Engine.Templates.Matching
{
    public class ExportMenuItemMatching : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => nameof(ExportMenuItemMatching);
        public override string Header => "模板匹配";
        public override int Order => 50;
        public override ITemplate Template => new TemplateMatch();
    }
}

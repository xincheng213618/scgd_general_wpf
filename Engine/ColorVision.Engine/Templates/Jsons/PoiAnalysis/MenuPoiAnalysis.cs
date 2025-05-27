using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.PoiAnalysis
{
    public class MenuPoiAnalysis : MenuITemplateAlgorithmBase
    {
        public override string Header => "POI分析";
        public override int Order => 1003;
        public override ITemplate Template => new TemplatePoiAnalysis();
    }

}

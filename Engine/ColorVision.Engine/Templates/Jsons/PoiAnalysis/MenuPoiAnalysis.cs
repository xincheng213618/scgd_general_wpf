using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.PoiAnalysis
{
    public class MenuPoiAnalysis : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.POIAnalysis;
        public override int Order => 1003;
        public override ITemplate Template => new TemplatePoiAnalysis();
    }

}

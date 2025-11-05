namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class MenuPoiFliterParam : MenuTemplatePoiBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.POIFilterTemplateSettings;
        public override int Order => 1;
        public override ITemplate Template => new TemplatePoiFilterParam();
    }
}

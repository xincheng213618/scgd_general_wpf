namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class MenuBuildPOI : MenuTemplatePoiBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.POIPlacementTemplateSettings;
        public override int Order => 5;

        public override ITemplate Template => new TemplateBuildPoi();
    }
}

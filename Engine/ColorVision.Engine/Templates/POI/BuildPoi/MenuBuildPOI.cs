namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class MenuBuildPOI : MenuTemplatePoiBase
    {
        public override string Header => "Poi布点模板设置";
        public override int Order => 5;

        public override ITemplate Template => new TemplateBuildPoi();
    }
}

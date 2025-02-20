namespace ColorVision.Engine.Templates.POI.POIRevise
{
    public class MenuPoiRevise : MenuTemplatePoiBase
    {
        public override string Header => "Poi修正模板设置";
        public override int Order => 3;
        public override ITemplate Template => new TemplatePoiReviseParam();
    }
}

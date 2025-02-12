namespace ColorVision.Engine.Templates.POI.POIGenCali
{
    public class MenuPoiGenCali : MenuTemplatePoiBase
    {
        public override string Header => "Poi修正标定参数模板设置";
        public override int Order => 2;
        public override ITemplate Template => new TemplatePoiGenCalParam();
    }
}

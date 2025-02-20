namespace ColorVision.Engine.Templates.POI.POIOutput
{
    public class MenuPoiOutput : MenuTemplatePoiBase
    {
        public override string Header => "Poi文件输出模板设置";
        public override int Order => 4;

        public override ITemplate Template => new TemplatePoiOutputParam();
    }
}

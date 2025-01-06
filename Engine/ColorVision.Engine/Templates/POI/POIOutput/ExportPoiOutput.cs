namespace ColorVision.Engine.Templates.POI.POIOutput
{
    public class ExportPoiOutput : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "PoiOutput";
        public override string Header => "Poi文件输出模板设置";
        public override int Order => 4;

        public override ITemplate Template => new TemplatePoiOutputParam();
    }
}

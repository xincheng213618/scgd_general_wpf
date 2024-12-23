namespace ColorVision.Engine.Templates.POI.POIRevise
{
    public class ExportPoiReviseParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "PoiRevise";
        public override string Header => "Poi修正模板设置";
        public override int Order => 3;
        public override ITemplate Template => new TemplatePoiReviseParam();
    }
}

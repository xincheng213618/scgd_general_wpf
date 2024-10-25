using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIFilters
{
    public class ExportPoiFliterParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "POIFliterParam";
        public override string Header => "POI过滤模板设置";
        public override int Order => 1;
        public override ITemplate Template => new TemplatePoiFilterParam();
    }
}

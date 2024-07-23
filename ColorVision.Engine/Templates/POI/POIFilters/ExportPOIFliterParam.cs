namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class ExportPOIFliterParam : ExportTemplateBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "POIFliterParam";
        public override string Header => "POIFliterParam";
        public override int Order => 9;
        public override ITemplate Template => new TemplatePOIFilterParam();
    }
}

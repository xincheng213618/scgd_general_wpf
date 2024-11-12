using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.POIGenCali
{
    public class ExportPoiGenCalParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "PoiGenCali";
        public override string Header => "Poi修正标定参数模板设置";
        public override int Order => 2;
        public override ITemplate Template => new TemplatePoiGenCalParam();
    }
}

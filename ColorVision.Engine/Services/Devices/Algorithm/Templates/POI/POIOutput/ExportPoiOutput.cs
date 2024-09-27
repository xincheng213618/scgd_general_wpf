using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.PoiOutput
{
    public class ExportPoiOutput : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplatePOI";
        public override string GuidId => "PoiOutput";
        public override string Header => "PoiOutput算法设置";
        public override int Order => 0;

        public override ITemplate Template => new TemplatePoiOutputParam();
    }
}

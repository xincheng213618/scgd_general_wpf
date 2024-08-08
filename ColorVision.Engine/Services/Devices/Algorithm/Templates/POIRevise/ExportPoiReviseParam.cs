using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIRevise
{
    public class ExportPoiReviseParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "PoiRevise";
        public override string Header => "Poi修正模板设置";
        public override int Order => 0;
        public override ITemplate Template => new TemplatePoiReviseParam();
    }
}

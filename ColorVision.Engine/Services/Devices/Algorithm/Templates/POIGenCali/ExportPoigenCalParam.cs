using ColorVision.Engine.Services.Devices.Algorithm.Templates.POIRevise;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POIGenCali
{
    public class ExportPoiGenCalParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "PoiGenCali";
        public override string Header => "PoiGenCali算法设置";
        public override int Order => 0;
        public override ITemplate Template => new TemplatePoiGenCalParam();
    }
}

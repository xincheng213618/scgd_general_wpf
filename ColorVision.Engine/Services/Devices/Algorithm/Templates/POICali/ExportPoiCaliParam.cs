using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POICali
{
    public class ExportPoiCaliParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "PoiCali";
        public override string Header => "PoiCali模板设置";
        public override int Order => 0;
        public override ITemplate Template => new TemplatePoiCaliParam();
    }
}

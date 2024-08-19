using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck2
{
    public class ExportLedCheck2Param : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "LedCheckParam2";
        public override string Header => "灯珠检测2";
        public override int Order => 2;
        public override ITemplate Template => new TemplateLedCheck2Param();
    }
}

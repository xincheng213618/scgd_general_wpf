using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class ExportLedCheck2 : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "LedCheck2";
        public override string Header => "灯珠检测2";
        public override int Order => 2;
        public override ITemplate Template => new TemplateLedCheck2();
    }
}

using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck3
{
    public class ExportLedCheck3 : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "LedCheck3";
        public override string Header => "灯珠检测3";
        public override int Order => 2;
        public override ITemplate Template => new TemplateThirdParty("LedCheck3");
    }
}

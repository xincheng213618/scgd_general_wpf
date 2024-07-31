using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.Manager
{
    public class ExportMenuItemThirdPartyAlgorithmsManager : ExportTemplateBase
    {
        public override string OwnerGuid => "ThirdPartyAlgorithms";
        public override string GuidId => "ThirdPartyAlgorithmsManager";
        public override string Header => "ThirdPartyAlgorithms";
        public override int Order => 9999;
        public override ITemplate Template => new TemplateThirdPartyManager();
    }
}

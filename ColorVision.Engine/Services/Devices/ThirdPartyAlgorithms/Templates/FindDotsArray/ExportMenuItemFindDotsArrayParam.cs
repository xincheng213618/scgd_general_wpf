using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates.FindDotsArray
{
    public class ExportMenuItemFindDotsArrayParam : ExportTemplateBase
    {
        public override string OwnerGuid => "ThirdPartyAlgorithms";
        public override string GuidId => "FindDotsArrayParam";
        public override string Header => "FindDotsArrayParam";
        public override ITemplate Template => new TemplateFindDotsArrayParam();
    }
}

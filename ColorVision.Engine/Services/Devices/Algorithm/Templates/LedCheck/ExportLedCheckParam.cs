using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.LedCheck
{
    public class ExportLedCheckParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "LedCheckParam";
        public override string Header => Properties.Resources.MenuLedCheck;
        public override int Order => 2;
        public override ITemplate Template => new TemplateLedCheckParam();
    }
}

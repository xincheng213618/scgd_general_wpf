using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.SFR
{
    public class ExportSFRParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "SFRParam";
        public override string Header => Properties.Resources.MenuSFR;
        public override int Order => 2;
        public override ITemplate Template => new TemplateSFRParam();
    }
}

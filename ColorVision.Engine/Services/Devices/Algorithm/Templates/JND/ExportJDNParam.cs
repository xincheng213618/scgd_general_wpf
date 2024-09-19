using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JND
{
    public class ExportJDNParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "JDNParam";
        public override string Header => Properties.Resources.MenuGhost;
        public override int Order => 3;
        public override ITemplate Template => new TemplateJNDParam();
    }
}

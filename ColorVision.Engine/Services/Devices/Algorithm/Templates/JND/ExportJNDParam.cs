using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JDN
{
    public class ExportJNDParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "JNDFParam";
        public override string Header => "JND";
        public override int Order => 3;
        public override ITemplate Template => new TemplateJDN();
    }
}

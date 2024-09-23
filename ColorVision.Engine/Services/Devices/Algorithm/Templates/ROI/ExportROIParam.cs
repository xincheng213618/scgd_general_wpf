using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.ROI
{
    public class ExportROIParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "JDNParam";
        public override string Header => "发光区检测2";
        public override int Order => 3;
        public override ITemplate Template => new TemplateROIParam();
    }
}

using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POICal
{
    public class ExportPOICalParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "POICal";
        public override string Header => "POICal模板设置";
        public override int Order => 0;
        public override ITemplate Template => new TemplatePOICalParam();
    }
}

using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.DataLoad
{
    public class ExportDataLoad : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "DataLoad";
        public override string Header => "数据加载模板设置";
        public override int Order => 0;
        public override ITemplate Template => new TemplateDataLoad();
    }
}

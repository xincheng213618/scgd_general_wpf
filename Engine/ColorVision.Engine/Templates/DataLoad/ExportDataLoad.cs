namespace ColorVision.Engine.Templates.DataLoad
{
    public class ExportDataLoad : MenuITemplateAlgorithmBase
    {
        public override string Header => "数据加载模板设置";
        public override int Order => 0;
        public override ITemplate Template => new TemplateDataLoad();
    }
}

namespace ColorVision.Engine.Templates.Jsons.DataConvert
{
    public class ExportDataConvert : MenuITemplateAlgorithmBase
    {
        public override string Header => "数据转换";
        public override int Order => 2;
        public override ITemplate Template => new TemplateDataConvert();
    }
}

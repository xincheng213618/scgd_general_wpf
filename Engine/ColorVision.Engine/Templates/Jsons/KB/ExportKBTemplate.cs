namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class ExportKBTemplate : MenuITemplateAlgorithmBase
    {
        public override string Header => "KB统一模板";
        public override int Order => 2003;
        public override ITemplate Template => new TemplateKB();
    }




}

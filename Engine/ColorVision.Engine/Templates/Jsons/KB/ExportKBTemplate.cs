namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class ExportKBTemplate : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => nameof(ExportKBTemplate);
        public override string Header => "KB统一模板";
        public override int Order => 2;
        public override ITemplate Template => new TemplateKB();
    }




}

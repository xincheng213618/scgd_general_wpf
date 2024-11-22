namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class ExportKBTemplate : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => nameof(ExportKBTemplate);
        public override string Header => "ExportKBTemplate";
        public override int Order => 2;
        public override ITemplate Template => new TemplateKB();
    }




}

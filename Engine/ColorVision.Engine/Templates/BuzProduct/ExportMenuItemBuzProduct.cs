namespace ColorVision.Engine.Templates.BuzProduct
{
    public class ExportMenuItemBuzProduct : ExportTemplateBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => nameof(TemplateBuzProduc);
        public override string Header => "TemplateBuzProduc";
        public override int Order => 2;
        public override ITemplate Template => new TemplateBuzProduc();
    }
}

namespace ColorVision.Engine.Templates.LedCheck
{
    public class ExportLedCheckParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "LedCheckParam";
        public override string Header => "灯珠检测1";
        public override int Order => 2;
        public override ITemplate Template => new TemplateLedCheck();
    }
}

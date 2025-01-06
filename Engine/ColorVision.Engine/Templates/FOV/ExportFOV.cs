namespace ColorVision.Engine.Templates.FOV
{
    public class ExportFOV : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "FOV";
        public override string Header => Properties.Resources.MenuFOV;
        public override int Order => 5;
        public override ITemplate Template => new TemplateFOV();
    }
}

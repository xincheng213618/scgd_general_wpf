namespace ColorVision.Engine.Templates.SFR
{
    public class ExportSFRParam : MenuITemplateAlgorithmBase
    {
        public override string Header => Properties.Resources.MenuSFR;
        public override int Order => 2;
        public override ITemplate Template => new TemplateSFR();
    }
}

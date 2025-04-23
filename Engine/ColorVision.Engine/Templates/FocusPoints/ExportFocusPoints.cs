namespace ColorVision.Engine.Templates.FocusPoints
{
    public class ExportFocusPoints : MenuITemplateAlgorithmBase
    {
        public override int Order => 2;
        public override string Header => "FocusPoints";
        public override ITemplate Template => new TemplateFocusPoints();
    }
}

namespace ColorVision.Engine.Templates.FocusPoints
{
    public class ExportFocusPoints : MenuITemplateAlgorithmBase
    {
        public override int Order => 2;
        public override string Header => Properties.Resources.MenuFocusPoints;
        public override ITemplate Template => new TemplateFocusPoints();
    }
}

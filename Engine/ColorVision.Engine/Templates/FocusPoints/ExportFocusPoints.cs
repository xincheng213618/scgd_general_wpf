namespace ColorVision.Engine.Templates.FocusPoints
{
    public class ExportFocusPoints : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "FocusPoints";
        public override int Order => 2;
        public override string Header => Properties.Resources.MenuFocusPoints;
        public override ITemplate Template => new TemplateFocusPoints();
    }
}

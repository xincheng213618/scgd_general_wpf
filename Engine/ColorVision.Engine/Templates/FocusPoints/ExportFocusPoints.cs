using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.FocusPoints
{
    [System.Obsolete("Algorithm-template menu entry removed from the main menu.")]
    public class ExportFocusPoints : MenuITemplateAlgorithmBase
    {
        public override int Order => 2;
        public override string Header => "FocusPoints";
        public override ITemplate Template => new TemplateFocusPoints();
    }
}

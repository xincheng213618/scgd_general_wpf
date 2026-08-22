using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.FindLightArea
{
    [System.Obsolete("Algorithm-template menu entry removed from the main menu.")]
    public class ExportRoi : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.AADetect;
        public override int Order => 3;
        public override ITemplate Template => new TemplateRoi();
    }
}

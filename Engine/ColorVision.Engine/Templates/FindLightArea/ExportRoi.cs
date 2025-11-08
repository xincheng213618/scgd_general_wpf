using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.FindLightArea
{
    public class ExportRoi : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.AADetect;
        public override int Order => 3;
        public override ITemplate Template => new TemplateRoi();
    }
}

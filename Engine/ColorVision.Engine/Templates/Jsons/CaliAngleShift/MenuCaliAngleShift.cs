using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.CaliAngleShift
{
    public class MenuCaliAngleShift : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.ColorCorrection;
        public override int Order => 1004;
        public override ITemplate Template => new TemplateCaliAngleShift();
    }

}

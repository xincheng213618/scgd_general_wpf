using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.CaliAngleShift
{
    public class MenuCaliAngleShift : MenuITemplateAlgorithmBase
    {
        public override string Header => "色差校正";
        public override int Order => 1004;
        public override ITemplate Template => new TemplateCaliAngleShift();
    }

}

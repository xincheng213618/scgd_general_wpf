using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.OLEDAOI
{
    public class MenuOLEDAOI : MenuITemplateAlgorithmBase
    {
        public override string Header => "OLED AOI";
        public override int Order => 1028;
        public override ITemplate Template => new TemplateOLEDAOI();
    }

}

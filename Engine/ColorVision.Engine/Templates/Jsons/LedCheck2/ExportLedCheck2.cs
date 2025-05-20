using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.LedCheck2
{
    public class ExportLedCheck2 : MenuITemplateAlgorithmBase
    {
        public override string Header => "亚像素级灯珠检测";
        public override int Order => 41325;
        public override ITemplate Template => new TemplateLedCheck2();

    }
}

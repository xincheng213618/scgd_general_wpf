using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.Jsons.KB
{
    public class ExportKBTemplate : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.KBDetect;
        public override int Order => 2003;
        public override ITemplate Template => new TemplateKB();
    }




}

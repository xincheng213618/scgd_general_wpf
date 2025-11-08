using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.DataLoad
{
    public class ExportDataLoad : MenuITemplateAlgorithmBase
    {
        public override string Header => ColorVision.Engine.Properties.Resources.DataLoad;
        public override int Order => 0;
        public override ITemplate Template => new TemplateDataLoad();
    }
}

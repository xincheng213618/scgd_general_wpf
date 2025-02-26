namespace ColorVision.Engine.Templates.MTF
{
    public class ExportMenuItemMTF : MenuITemplateAlgorithmBase
    {
        public override string Header => Properties.Resources.MenuMTF;
        public override int Order => 2;
        public override ITemplate Template => new TemplateMTF();
    }
}

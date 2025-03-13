namespace ColorVision.Engine.Templates.MTF
{
    public class ExportMenuItemMTF : MenuITemplateAlgorithmBase
    {
        public override string Header => "MTF";
        public override int Order => 1002;
        public override ITemplate Template => new TemplateMTF();
    }
}

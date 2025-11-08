using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class MenuIBuzProduct : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string GuidId => nameof(TemplateBuzProduc);
        public override string Header => ColorVision.Engine.Properties.Resources.ApplicationPropertyTemplate;
        public override int Order => 4;
        public override ITemplate Template => new TemplateBuzProduc();
    }
}

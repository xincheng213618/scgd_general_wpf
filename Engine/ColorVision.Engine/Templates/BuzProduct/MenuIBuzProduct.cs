using ColorVision.Engine.Templates.Menus;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class MenuIBuzProduct : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string GuidId => nameof(TemplateBuzProduc);
        public override string Header => "应用属性模板";
        public override int Order => 4;
        public override ITemplate Template => new TemplateBuzProduc();
    }
}

using ColorVision.UI.Menus;

namespace ColorVision.Engine
{
    public class ProductBrochure : MenuItemBase, IMenuItem
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "ProductBrochure";

        public override int Order => 99999;
        public override string Header => ColorVision.Engine.Properties.Resources.ProductBrochure;

        public override void Execute()
        {
            if (UI.Languages.LanguageConfig.Instance.UICulture == "en")
            {
                Common.Utilities.PlatformHelper.Open(@"Assets\Catalog\Catalog-宣传册EN.pdf");
            }
            else
            {
                Common.Utilities.PlatformHelper.Open(@"Assets\Catalog\Catalog宣传册.pdf");
            }
        }
    }
}

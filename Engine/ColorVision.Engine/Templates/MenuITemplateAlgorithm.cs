using ColorVision.UI.Menus;

namespace ColorVision.Engine.Templates
{
    public class MenuITemplateAlgorithm : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string Header => Properties.Resources.MenuAlgorithm;
        public override int Order => 3;
    }

    public abstract class MenuITemplateAlgorithmBase : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuITemplateAlgorithm);
    }


}

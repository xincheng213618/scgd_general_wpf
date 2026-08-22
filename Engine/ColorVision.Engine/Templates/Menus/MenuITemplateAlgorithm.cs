using ColorVision.UI.Menus;

namespace ColorVision.Engine.Templates.Menus
{
    [System.Obsolete("Algorithm-template menu removed from the main menu.")]
    public class MenuITemplateAlgorithm : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string Header => Properties.Resources.MenuAlgorithm;
        public override int Order => 3;
    }

    [System.Obsolete("Algorithm-template menu entries removed from the main menu.")]
    public abstract class MenuITemplateAlgorithmBase : MenuItemTemplateBase
    {
        public override string OwnerGuid => nameof(MenuITemplateAlgorithm);
    }


}

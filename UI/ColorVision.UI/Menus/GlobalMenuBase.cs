namespace ColorVision.UI.Menus
{
    /// <summary>
    /// 全局菜单基类，所有窗口都会加载的菜单项应该继承自这个类
    /// </summary>
    public abstract class GlobalMenuBase : MenuItemBase
    {
        public override string TargetName => MenuItemConstants.GlobalTarget;

        public override string OwnerGuid => MenuItemConstants.Menu;
    }
}

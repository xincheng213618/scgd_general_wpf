namespace ColorVision.UI
{
    public interface IMenuService
    {
        void RefreshMenuItemsByGuid(string ownerGuid);
    }

    public class MenuService
    {
        public static IMenuService Instance { get; private set; }
        public static void SetInstance(IMenuService instance)
        {
            Instance = instance;
        }
    }

}

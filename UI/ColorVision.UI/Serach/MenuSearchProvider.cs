using ColorVision.UI.Menus;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus.Base.File;
using log4net;
using System.Windows;

namespace ColorVision.UI.Serach
{
    public class MenuSearchProvider: ISearchProvider
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MenuSearchProvider));
        public MenuSearchProvider() { }

        public IEnumerable<ISearch> GetSearchItems()
        {
            var results = new List<ISearch>();
            foreach (var item in MenuManager.GetInstance().GetAllMenuItemsFiltered())
            {
                try
                {
                    SearchMeta? search = CreateSearchItem(item);
                    if (search != null) results.Add(search);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    Log.Warn($"Read search menu metadata failed: {item.GetType().FullName}", exception);
                }
            }
            return results;
        }

        internal static SearchMeta? CreateSearchItem(IMenuItem item)
        {
            // A main-window command palette must not invoke commands belonging to another editor window.
            if (item.TargetName != MenuItemConstants.MainWindowTarget && item.TargetName != MenuItemConstants.GlobalTarget
                || item.OwnerGuid == MenuItemConstants.Menu || item.Visibility != Visibility.Visible
                || string.IsNullOrWhiteSpace(item.Header) || item.Command == null) return null;

            HotKeys presentation = new() { Name = item.Header };
            string? actionId = null;
            if (item is IHotKey provider)
            {
                presentation = provider.HotKeys;
                actionId = !string.IsNullOrWhiteSpace(presentation.Id) ? presentation.Id
                    : item is IHotkeyProvider ? null : item.GetType().FullName ?? item.GetType().Name;
            }
            HotkeyPresentationInfo info = HotkeyPresentation.For(HotkeyPresentation.Enrich(presentation, item));
            string description = info.Description == HotkeyPresentation.GetText("DescriptionUnavailable")
                ? $"{info.Category} › {SearchResultItem.CleanTitle(item.Header)}" : info.Description;
            return new SearchMeta
            {
                Type = SearchType.Menu,
                GuidId = $"menu:{item.TargetName}:{item.GuidId ?? item.GetType().FullName}",
                Header = item.Header,
                Description = description,
                CategoryKey = "Commands",
                Aliases = [info.Name, info.Category, item.GuidId ?? string.Empty],
                ActionId = actionId,
                // MenuClose's menu/hotkey adapter follows the active window. Search
                // owns a separate window, so use the document host's explicit route.
                Command = item is MenuClose ? MenuClose.CloseDocumentCommand : item.Command,
            };
        }
    }
}

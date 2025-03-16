using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.Flow
{
    public class SearchProvider: ISearchProvider
    {
        public SearchProvider() { }

        public IEnumerable<ISearch> GetSearchItems()
        {
            List<IMenuItem> MenuItems = new List<IMenuItem>();

            foreach (var item in TemplateFlow.Params)
            {
                SearchMeta search = new SearchMeta
                {
                    Type = SearchType.File,
                    Header = item.Key,
                    Command = new RelayCommand(a =>
                    {
                        new FlowEngineToolWindow(item.Value) { Owner = Application.Current.GetActiveWindow() }.Show();
                    })
                };
                yield return search;

            }
        }
    }
}

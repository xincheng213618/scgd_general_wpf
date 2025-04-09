using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public class SearchProvider: ISearchProvider
    {
        public SearchProvider() { }

        public IEnumerable<ISearch> GetSearchItems()
        {
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

            foreach (var item in TemplatePoi.Params)
            {
                SearchMeta search = new SearchMeta
                {
                    Type = SearchType.File,
                    Header = item.Key,
                    Command = new RelayCommand(a =>
                    {
                        new EditPoiParam(item.Value) { Owner = Application.Current.GetActiveWindow() }.Show();
                    })
                };
                yield return search;
            }
            foreach (var item in TemplateKB.Params)
            {
                SearchMeta search = new SearchMeta
                {
                    Type = SearchType.File,
                    Header = item.Key,
                    Command = new RelayCommand(a =>
                    {
                        new EditPoiParam1(item.Value) { Owner = Application.Current.GetActiveWindow() }.Show();
                    })
                };
                yield return search;
            }
        }
    }
}

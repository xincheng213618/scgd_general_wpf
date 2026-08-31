using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public class TemplateSearchProvider: ISearchProvider
    {
        public IEnumerable<ISearch> GetSearchItems()
        {
            return CreateItems(TemplateControl.ITemplateNames.ToArray(),
                key => TemplateControl.ITemplateNames.TryGetValue(key, out ITemplate? template) ? template : null);
        }

        internal static IEnumerable<ISearch> CreateItems(IEnumerable<KeyValuePair<string, ITemplate>> registrations, Func<string, ITemplate?> resolve)
        {
            foreach (KeyValuePair<string, ITemplate> registration in registrations)
            {
                string registrationKey = registration.Key;
                ITemplate registered = registration.Value;
                foreach (string name in registered.GetTemplateNames().Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    yield return new SearchMeta
                    {
                        GuidId = $"template:{Uri.EscapeDataString(registrationKey)}:{Uri.EscapeDataString(name)}",
                        Type = SearchType.File,
                        CategoryKey = "Templates",
                        Header = name,
                        Description = string.IsNullOrWhiteSpace(registered.Title) ? registered.Name : registered.Title,
                        Aliases = new[] { registrationKey, registered.Code ?? string.Empty, registered.GetType().Name },
                        Command = new RelayCommand(_ =>
                        {
                            ITemplate? template = resolve(registrationKey);
                            if (template == null || !template.GetTemplateNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return;
                            int index = template.GetTemplateIndex(name);
                            if (index < 0) return;
                            if (template.IsSideHide)
                            {
                                template.PreviewMouseDoubleClick(index);
                            }
                            else
                            {
                                new TemplateEditorWindow(template, index) { Owner = Application.Current.GetActiveWindow() }.Show();
                            }
                        }, _ => resolve(registrationKey)?.GetTemplateNames().Contains(name, StringComparer.OrdinalIgnoreCase) == true)
                    };
                }
            }
        }
    }
}

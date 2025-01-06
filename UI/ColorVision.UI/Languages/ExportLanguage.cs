using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Languages
{
    public class ExportLanguage : IMenuItemMeta
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "MenuMenuLanguage";

        public int Order => 1001;

        public string? Header => Properties.Resources.MenuLanguage;

        public string? InputGestureText => "Ctrl + Shift + L";

        public object? Icon => null;
        public ICommand Command => null;
        public Visibility Visibility => Visibility.Visible;

        public MenuItem MenuItem
        {
            get
            {
                MenuItem MenuLanguage = new() { Header = Header, InputGestureText = InputGestureText };

                foreach (var item in LanguageManager.Current.Languages)
                {
                    MenuItem LanguageItem = new();
                    LanguageItem.Header = LanguageManager.keyValuePairs.TryGetValue(item, out string value) ? value : item;
                    LanguageItem.Click += (s, e) =>
                    {
                        string temp = Thread.CurrentThread.CurrentUICulture.Name;
                        LanguageConfig.Instance.UICulture = item;
                        bool sucess = LanguageManager.Current.LanguageChange(item);
                        if (!sucess)
                        {
                            LanguageConfig.Instance.UICulture = temp;
                        }
                    };
                    LanguageItem.Tag = item;
                    LanguageItem.IsChecked = Thread.CurrentThread.CurrentUICulture.Name == item;
                    MenuLanguage.Items.Add(LanguageItem);
                }


                MenuLanguage.Loaded += (s, e) =>
                {
                    foreach (var item in MenuLanguage.Items)
                    {
                        if (item is MenuItem LanguageItem && LanguageItem.Tag is string Language)
                            LanguageItem.IsChecked = Thread.CurrentThread.CurrentUICulture.Name == Language;
                    }
                };

                return MenuLanguage;
            }
        }
    }
}

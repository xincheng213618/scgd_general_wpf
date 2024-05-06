using ColorVision.Common.MVVM;
using ColorVision.Language;
using ColorVision.UI;
using System.Threading;
using System.Windows.Controls;

namespace ColorVision.Settings
{
    public class ExportLanguage : IMenuItemMeta
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "MenuMenuLanguage";

        public int Order => 1001;

        public string? Header => Properties.Resource.MenuLanguage;

        public string? InputGestureText => "Ctrl + Shift + L";

        public object? Icon => null;
        public RelayCommand Command => new RelayCommand(a => { });

        public MenuItem MenuItem
        {
            get
            {
                MenuItem MenuLanguage = new MenuItem { Header = Header, InputGestureText = InputGestureText };

                foreach (var item in LanguageManager.Current.Languages)
                {
                    MenuItem LanguageItem = new MenuItem();
                    LanguageItem.Header = LanguageManager.keyValuePairs.TryGetValue(item, out string value) ? value : item;
                    LanguageItem.Click += (s, e) =>
                    {
                        string temp = Thread.CurrentThread.CurrentUICulture.Name;
                        ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.UICulture = item;
                        ConfigHandler.GetInstance().SaveConfig();
                        bool sucess = LanguageManager.Current.LanguageChange(item);
                        if (!sucess)
                        {
                            ConfigHandler.GetInstance().SoftwareConfig.SoftwareSetting.UICulture = temp;
                            ConfigHandler.GetInstance().SaveConfig();
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

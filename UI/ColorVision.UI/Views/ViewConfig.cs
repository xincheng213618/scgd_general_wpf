using ColorVision.Common.MVVM;
using ColorVision.UI.Configs;
using ColorVision.UI.Menus;
using ColorVision.UI.Properties;
using ColorVision.UI.PropertyEditor;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Views
{
    public class ViewConfigSettingProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = Resources.AutoSwitchSelectedView,
                                Description = Resources.AutoSwitchSelectedView,
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(ViewConfig.IsAutoSelect),
                                Source = ViewConfig.Instance
                            }
            };
        }
    }

    public class ExportViewConfig : IMenuItemMeta
    {
        public string? OwnerGuid => "View";
        public string? GuidId => "ViewCount";
        public int Order => 10000;
        public string? Header => "ViewCount";
        public MenuItem MenuItem
        {
            get
            {
                MenuItem MenuViews = new() { Header = Header };
                for (int i = 1; i < 5; i++)
                {
                    int tag = i;
                    MenuItem menuItem = new MenuItem() { Header = i };
                    menuItem.Click += (s, e) =>
                    {
                        ViewGridManager.GetInstance().SetViewGrid(tag);
                    };
                    MenuViews.Items.Add(menuItem);
                }
                return MenuViews;
            }
        }
        public string? InputGestureText => null;
        public object? Icon => null;
        public ICommand? Command => null;
        public Visibility Visibility => Visibility.Visible;
    }

    [DisplayName("视图配置")]
    public class ViewConfig : ViewModelBase, IConfig
    {
        public static ViewConfig Instance => ConfigService.Instance.GetRequiredService<ViewConfig>();

        public RelayCommand EditCommand { get; set; }

        public ViewConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
        public int LastViewCount { get => _LastViewCount; set { _LastViewCount = value; NotifyPropertyChanged(); } }
        private int _LastViewCount = 1;

    }
}

using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.UI.Sorts;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;

namespace Spectrum
{
    public class MainWindowConfig : WindowConfig,IConfig,IConfigSettingProvider,IFullScreenState
    {
        public static MainWindowConfig Instance => ConfigService.Instance.GetRequiredService<MainWindowConfig>();

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public bool LogControlVisibility { get => _LogControlVisibility; set { _LogControlVisibility  =value;OnPropertyChanged(); } }
        private bool _LogControlVisibility = true;
        [JsonIgnore]
        public bool IsFull { get => _IsFull; set { _IsFull = value; OnPropertyChanged(); } }
        private bool _IsFull;

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            var list = new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name ="界面显示日志",
                    Description =  "界面显示日志",
                    Order = 1,
                    BindingName =nameof(LogControlVisibility),
                    Source = this,
                }
            };
            return list;
        }
    }
    public class ExportMenuViewMax : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "全屏";

        public override void Execute()
        {
            MainWindowConfig.Instance.IsFull = !MainWindowConfig.Instance.IsFull;
            MenuManager.GetInstance().RefreshMenuItemsByGuid(OwnerGuid);
        }
        public override bool? IsChecked => MainWindowConfig.Instance.IsFull ? true : null;
    }
}

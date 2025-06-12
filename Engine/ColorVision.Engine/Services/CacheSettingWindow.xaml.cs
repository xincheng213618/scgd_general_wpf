using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    public class MenuCacheSetting : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "缓存管理";
        public override int Order => 3;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new CacheSettingWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    public class CacheSettingInfo
    {
        public RelayCommand EditCommand { get; set; }

        public CacheSettingInfo()
        {
            EditCommand = new RelayCommand(a => Edit());
        }
        public void Edit()
        {
            var oldvalue = FileServerCfg.FileServerCfg.Clone();

            var window = new PropertyEditorWindow(FileServerCfg.FileServerCfg, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            window.Closed += (s, e) =>
            {
                if (!FileServerCfg.FileServerCfg.EqualMax(oldvalue))
                {
                    DeviceService.Save();
                }
            };
            window.ShowDialog();
        }

        public DeviceService DeviceService { get; set; }

        public IFileServerCfg FileServerCfg { get; set; }

        public string Name { get; set; }

    }



    public class CacheSettingManager
    {
        private static CacheSettingManager _instance;
        private static readonly object _locker = new();
        public static CacheSettingManager GetInstance() { lock (_locker) { return _instance ??= new CacheSettingManager(); } }

        public ObservableCollection<CacheSettingInfo> CacheSettingInfos { get; set; }
        public RelayCommand UnifiedEditCommand { get; set; }



        public CacheSettingManager()
        {
            UnifiedEditCommand = new RelayCommand(a => UnifiedEdit());
            CacheSettingInfos = new ObservableCollection<CacheSettingInfo>();
            Init();
            ServiceManager.GetInstance().ServiceChanged += (s, e) => Init();
        }

        public void UnifiedEdit()
        {
            if (CacheSettingInfos.Count == 0)
            {
                MessageBox.Show("没有服务需要配置缓存路径","ColorVision");
            }
            var Unifiedvalue = CacheSettingInfos[0].FileServerCfg.FileServerCfg.Clone();


            var window = new PropertyEditorWindow(Unifiedvalue, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            window.Closed += (s, e) =>
            {
                foreach (var item in CacheSettingInfos)
                {
                    if(!Unifiedvalue.EqualMax(item.FileServerCfg.FileServerCfg))
                    {
                        item.FileServerCfg.FileServerCfg.CopyFrom(Unifiedvalue);
                        item.DeviceService.Save();
                    }
                }
            };
            window.ShowDialog();

        }

        public void Init()
        {
            CacheSettingInfos.Clear();
            foreach (var service in ServiceManager.GetInstance().DeviceServices)
            {
                if (service.GetConfig() is IFileServerCfg fileServerCfg)
                {
                    CacheSettingInfos.Add(new CacheSettingInfo() { FileServerCfg = fileServerCfg, Name = service.Name, DeviceService = service });
                }
            }
        }
    }


    /// <summary>
    /// CacheSettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class CacheSettingWindow : Window
    {
        public CacheSettingWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = CacheSettingManager.GetInstance();
        }

        private void ListViewProjects_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}

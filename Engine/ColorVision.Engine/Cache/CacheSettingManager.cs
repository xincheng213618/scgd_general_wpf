using ColorVision.Common.MVVM;
using ColorVision.Engine.Cache;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Services
{
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
}

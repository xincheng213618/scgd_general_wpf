using ColorVision.Common.MVVM;
using ColorVision.Engine.Cache;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.Services
{
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
}

using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Services
{
    public class ServicesSetting:ViewModelBase, IConfig
    {
        public static ServicesSetting Instance => ConfigHandler.GetInstance().GetRequiredService<ServicesSetting>();

        public int ShowType { get; set; }

        public bool IsDefaultOpenService { get => _IsDefaultOpenService; set { _IsDefaultOpenService = value; NotifyPropertyChanged(); } }
        private bool _IsDefaultOpenService;
    }
}

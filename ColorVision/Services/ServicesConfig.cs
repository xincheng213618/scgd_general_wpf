using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ColorVision.Services
{
    public class ServicesConfig : ViewModelBase, IConfig
    {
        public static ServicesConfig Instance => ConfigHandler.GetInstance().GetRequiredService<ServicesConfig>();

        public int ShowType { get; set; }

        public bool IsDefaultOpenService { get => _IsDefaultOpenService; set { _IsDefaultOpenService = value; NotifyPropertyChanged(); } }
        private bool _IsDefaultOpenService;

        public bool IsRetorePlayControls { get => _IsRetorePlayControls; set { _IsRetorePlayControls = value; NotifyPropertyChanged(); } }
        private bool _IsRetorePlayControls = true;

        public Dictionary<string, int> PlayControls { get; set; } = new Dictionary<string, int>();
    }
}

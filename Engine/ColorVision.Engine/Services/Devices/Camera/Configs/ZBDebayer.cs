using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Camera.Configs
{
    /// <summary>
    /// 追加的class
    /// </summary>
    public class ZBDebayer:ViewModelBase
    {
        [DisplayName("ZBDebayerIsEnabled")]
        public bool IsEnabled { get => _IsEnabled; set { _IsEnabled = value; OnPropertyChanged(); } }
        private bool _IsEnabled;

        [DisplayName("ZBDebayerMethod"),PropertyVisibility(nameof(IsEnabled))]
        public int Method { get => _Method; set { _Method = value; OnPropertyChanged(); } }
        private int _Method = 1;
        [DisplayName("ZBDebayerChanneltype"), PropertyVisibility(nameof(IsEnabled))]
        public int Channeltype { get => _Channeltype; set { _Channeltype = value; OnPropertyChanged(); } }
        private int _Channeltype = 3;

    }


}
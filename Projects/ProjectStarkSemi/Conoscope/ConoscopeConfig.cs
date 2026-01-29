using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;

namespace ProjectStarkSemi.Conoscope
{
    public class ConoscopeConfig : ViewModelBase, IConfig
    {
        public ConoscopeModelType CurrentModel { get => _CurrentModel; set { if (_CurrentModel == value) return;  _CurrentModel = value; OnPropertyChanged(); ModelTypeChanged?.Invoke(this, _CurrentModel); } }
        private ConoscopeModelType _CurrentModel = ConoscopeModelType.VA80;

        public event EventHandler<ConoscopeModelType> ModelTypeChanged;


        public double ConoscopeCoefficient { get => _ConoscopeCoefficient; set { _ConoscopeCoefficient = value; OnPropertyChanged(); } }
        private double _ConoscopeCoefficient = 0.02645;


        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<double> DefaultAngles { get => _DefaultAngles; set { _DefaultAngles = value; OnPropertyChanged(); } }
        private ObservableCollection<double> _DefaultAngles = new ObservableCollection<double>() { 0,20,40,90,110,130,150 };

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<double> DefaultRAngles { get => _DefaultRAngles; set { _DefaultRAngles = value; OnPropertyChanged(); } }
        private ObservableCollection<double> _DefaultRAngles = new ObservableCollection<double>() {10 ,20,30,40,50,60,70,80};

        public ReferenceLineParam ReferenceLineParam { get => _ReferenceLineParam; set { _ReferenceLineParam = value; OnPropertyChanged(); } }
        private ReferenceLineParam _ReferenceLineParam = new ReferenceLineParam();

        public bool IsShowRedChannel { get => _IsShowRedChannel; set { _IsShowRedChannel = value; OnPropertyChanged(); } }
        private bool _IsShowRedChannel;
        public bool IsShowGreenChannel { get => _IsShowGreenChannel; set { _IsShowGreenChannel = value; OnPropertyChanged(); } }
        private bool _IsShowGreenChannel ;

        public bool IsShowBlueChannel { get => _IsShowBlueChannel; set { _IsShowBlueChannel = value; OnPropertyChanged(); } }
        private bool _IsShowBlueChannel;

        public bool IsShowXChannel { get => _IsShowXChannel; set { _IsShowXChannel = value; OnPropertyChanged(); } }
        private bool _IsShowXChannel;

        public bool IsShowYChannel { get => _IsShowYChannel; set { _IsShowYChannel = value; OnPropertyChanged(); } }
        private bool _IsShowYChannel = true;

        public bool IsShowZChannel { get => _IsShowZChannel; set { _IsShowZChannel = value; OnPropertyChanged(); } }
        private bool _IsShowZChannel ;

        /// <summary>
        /// 是否允许多选通道显示
        /// true: 允许多选（当前行为）
        /// false: 只允许单选（互斥切换模式）
        /// </summary>
        public bool AllowMultipleChannelSelection { get => _AllowMultipleChannelSelection; set { _AllowMultipleChannelSelection = value; OnPropertyChanged(); } }
        private bool _AllowMultipleChannelSelection = true;
    }
}

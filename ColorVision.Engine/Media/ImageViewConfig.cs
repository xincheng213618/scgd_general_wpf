using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Media
{
    public class ImageViewConfig:ViewModelBase
    {
        public double MaxZoom { get => _MaxZoom; set { _MaxZoom = value; NotifyPropertyChanged(); } }
        private double _MaxZoom = 10;
        public double MinZoom { get => _MinZoom; set { _MinZoom = value; NotifyPropertyChanged(); } }
        private double _MinZoom = 0.01;

        public int CVCIENum { get => _CVCIENum; set { _CVCIENum = value; NotifyPropertyChanged(); } }
        private int _CVCIENum = 200;

        [JsonIgnore]
        public string FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string _FilePath;
        [JsonIgnore]
        public bool ConvertXYZhandleOnce { get; set; }

        [JsonIgnore]
        public IntPtr ConvertXYZhandle { get; set; } = Tool.GenerateRandomIntPtr();

        [JsonIgnore]
        public bool ConvertXYZSetBuffer { get; set; } = false;


        [JsonIgnore]
        public bool IsCVCIE { get => _IsCVCIE; set { _IsCVCIE = value; NotifyPropertyChanged(); }  }
        private bool _IsCVCIE;

        [JsonIgnore]
        public int Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsChannel1));} }
        private int _Channel;

        [JsonIgnore]
        public bool IsChannel1 => Channel == 1;

        public int Ochannel { get; set; }

        public bool IsShowLoadImage { get => _IsShowLoadImage; set { _IsShowLoadImage = value; NotifyPropertyChanged(); } }
        private bool _IsShowLoadImage = true;

        public ColormapTypes ColormapTypes { get => _ColormapTypes; set { _ColormapTypes = value; NotifyPropertyChanged(); } }
        private ColormapTypes _ColormapTypes = ColormapTypes.COLORMAP_JET;
    }
}

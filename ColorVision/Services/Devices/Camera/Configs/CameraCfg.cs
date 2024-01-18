using Newtonsoft.Json;
using ColorVision.MVVM;
using System.Windows;

namespace ColorVision.Services.Devices.Camera.Configs
{
    public enum CameraConfigType
    {
        Camera = 0,
        ExpTime = 1,
        Calibration = 2,
        Channels = 3,
        SYSTEM = 4,
    };

    public class CameraCfg : ViewModelBase
    {
        /// <summary>
        /// 不参与计算的区域，左
        /// </summary>
        [JsonProperty("ob")]
        public int Ob { get => _Ob; set { _Ob = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(OBRect)); } }
        private int _Ob = 4;

        /// <summary>
        /// 不参与计算的区域，右
        /// </summary>
        [JsonProperty("obR")]
        public int ObR { get => _ObR; set { _ObR = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(OBRect)); } }
        private int _ObR;

        /// <summary>
        /// 不参与计算的区域，Top
        /// </summary>
        [JsonProperty("obT")]
        public int ObT { get => _ObT; set { _ObT = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(OBRect)); } }
        private int _ObT;

        /// <summary>
        /// 不参与计算的区域，下
        /// </summary>
        [JsonProperty("obB")]
        public int ObB { get => _ObB; set { _ObB = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(OBRect)); } }
        private int _ObB;

        /// <summary>
        /// 不参与计算的区域
        /// </summary>
        [JsonIgnore]
        public Rect OBRect { get => new Rect(Ob, ObR, ObT, ObB); }


        /// <summary>
        /// 温控
        /// </summary>
        [JsonProperty("tempCtlChecked")]
        public bool TempCtlChecked { get => _TempCtlChecked; set { _TempCtlChecked = value; NotifyPropertyChanged(); } }
        private bool _TempCtlChecked = true;

        /// <summary>
        /// 目标温度
        /// </summary>
        [JsonProperty("targetTemp")]
        public float TargetTemp { get => _TargetTemp; set { _TargetTemp = value; NotifyPropertyChanged(); } }
        private float _TargetTemp = -5.0f;

        /// <summary>
        /// 传输速率
        /// </summary>
        [JsonProperty("usbTraffic")]
        public float UsbTraffic { get => _UsbTraffic; set { _UsbTraffic = value; NotifyPropertyChanged(); } }
        private float _UsbTraffic;

        /// <summary>
        /// 偏移
        /// </summary>
        [JsonProperty("offset")]
        public int Offset { get => _Offset; set { _Offset = value; NotifyPropertyChanged(); } }
        private int _Offset;




        /// <summary>
        /// 增益
        /// </summary>
        [JsonProperty("gain")]
        public int Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private int _Gain = 10;

        /// <summary>
        /// OB
        /// </summary>
        [JsonIgnore]
        public Rect ROIRect { get => new Rect(PointX, PointY, Width, Height); }

        /// <summary>
        /// ROI X
        /// </summary>
        [JsonProperty("ex")]
        public int PointX { get => _PointX; set { _PointX = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ROIRect)); } }
        private int _PointX;
        /// <summary>
        /// ROI Y
        /// </summary>
        [JsonProperty("ey")]
        public int PointY { get => _PointY; set { _PointY = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ROIRect)); } }
        private int _PointY;
        /// <summary>
        /// ROI W
        /// </summary>
        [JsonProperty("ew")]
        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ROIRect)); } }
        private int _Width;
        /// <summary>
        /// ROI H
        /// </summary>
        [JsonProperty("eh")]
        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ROIRect)); } }
        private int _Height;

    }
}
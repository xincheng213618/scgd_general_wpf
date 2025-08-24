using Newtonsoft.Json;
using ColorVision.Common.MVVM;
using System.Windows;
using System.ComponentModel;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public enum CameraConfigType
    {
        Camera = 0,
        ExpTime = 1,
        Calibration = 2,
        Channels = 3,
        SYSTEM = 4,
    };

    [DisplayName("相机参数")]
    public class PhyCameraCfg : ViewModelBase
    {
        /// <summary>
        /// 不参与计算的区域，左
        /// </summary>
        [JsonProperty("ob")]
        [DisplayName("obL")]
        public int Ob { get => _Ob; set { _Ob = value; OnPropertyChanged();  } }
        private int _Ob = 4;

        /// <summary>
        /// 不参与计算的区域，右
        /// </summary>
        [JsonProperty("obR")]
        [DisplayName("obR")]
        public int ObR { get => _ObR; set { _ObR = value; OnPropertyChanged(); } }
        private int _ObR;

        /// <summary>
        /// 不参与计算的区域，Top
        /// </summary>
        [JsonProperty("obT")]
        [DisplayName("obT")]
        public int ObT { get => _ObT; set { _ObT = value; OnPropertyChanged();} }
        private int _ObT;

        /// <summary>
        /// 不参与计算的区域，下
        /// </summary>
        [JsonProperty("obB")]
        [DisplayName("obB")]
        public int ObB { get => _ObB; set { _ObB = value; OnPropertyChanged(); } }
        private int _ObB;


        /// <summary>
        /// 温控
        /// </summary>
        [JsonProperty("tempCtlChecked")]
        [DisplayName("温控")]
        public bool TempCtlChecked { get => _TempCtlChecked; set { _TempCtlChecked = value; OnPropertyChanged(); } }
        private bool _TempCtlChecked = true;

        /// <summary>
        /// 目标温度
        /// </summary>
        [JsonProperty("targetTemp")]
        [DisplayName("目标温度"), PropertyVisibility(nameof(TempCtlChecked))]
        public float TargetTemp { get => _TargetTemp; set { _TargetTemp = value; OnPropertyChanged(); } }
        private float _TargetTemp = 10.0f;
        /// <summary>
        /// 温度查询时间间隔
        /// </summary>
        [DisplayName("温度查询时间间隔(s)"), PropertyVisibility(nameof(TempCtlChecked))]
        public int TempSpanTime { get; set; } = 60;

        /// <summary>
        /// 传输速率
        /// </summary>
        [JsonProperty("usbTraffic")]
        [DisplayName("传输速率")]
        public float UsbTraffic { get => _UsbTraffic; set { _UsbTraffic = value; OnPropertyChanged(); } }
        private float _UsbTraffic;

        /// <summary>
        /// 偏移
        /// </summary>
        [JsonProperty("offset")]
        [DisplayName("偏移")]
        public int Offset { get => _Offset; set { _Offset = value; OnPropertyChanged(); } }
        private int _Offset;

        /// <summary>
        /// 增益
        /// </summary>
        [JsonProperty("gain")]
        [DisplayName("增益")]
        public int Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }
        private int _Gain = 10;

        /// <summary>
        /// FindLightArea X
        /// </summary>
        [JsonProperty("ex")]
        [DisplayName("ROI X"),Browsable(true)]
        public int PointX { get => _PointX; set { _PointX = value; OnPropertyChanged();  } }
        private int _PointX;
        /// <summary>
        /// FindLightArea Y
        /// </summary>
        [JsonProperty("ey")]
        [DisplayName("ROI Y"), Browsable(true)]
        public int PointY { get => _PointY; set { _PointY = value; OnPropertyChanged();  } }
        private int _PointY;
        /// <summary>
        /// FindLightArea W
        /// </summary>
        [JsonProperty("ew")]
        [DisplayName("ROI W"), Browsable(true)]
        public int Width { get => _Width; set { _Width = value; OnPropertyChanged();  } }
        private int _Width;
        /// <summary>
        /// FindLightArea H
        /// </summary>
        [JsonProperty("eh")]
        [DisplayName("ROI H"), Browsable(true)]
        public int Height { get => _Height; set { _Height = value; OnPropertyChanged(); } }
        private int _Height;


    }
}
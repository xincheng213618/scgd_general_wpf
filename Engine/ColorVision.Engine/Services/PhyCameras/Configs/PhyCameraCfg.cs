using Newtonsoft.Json;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Utilities;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{

    [LocalizedDisplayName(nameof(Resources.CameraParam))]
    public class PhyCameraCfg : ViewModelBase
    {
        /// <summary>
        /// 不参与计算的区域，左
        /// </summary>
        [JsonProperty("ob")]
        public int Ob { get => _Ob; set { _Ob = value; OnPropertyChanged();  } }
        private int _Ob = 4;

        /// <summary>
        /// 不参与计算的区域，右
        /// </summary>
        [JsonProperty("obR")]
        public int ObR { get => _ObR; set { _ObR = value; OnPropertyChanged(); } }
        private int _ObR;

        /// <summary>
        /// 不参与计算的区域，Top
        /// </summary>
        [JsonProperty("obT")]
        public int ObT { get => _ObT; set { _ObT = value; OnPropertyChanged();} }
        private int _ObT;

        /// <summary>
        /// 不参与计算的区域，下
        /// </summary>
        [JsonProperty("obB")]
        public int ObB { get => _ObB; set { _ObB = value; OnPropertyChanged(); } }
        private int _ObB;


        /// <summary>
        /// 温控
        /// </summary>
        [JsonProperty("tempCtlChecked")]
        [LocalizedDisplayName(nameof(Resources.TemperatureCtrl))]
        public bool TempCtlChecked { get => _TempCtlChecked; set { _TempCtlChecked = value; OnPropertyChanged(); } }
        private bool _TempCtlChecked = true;

        /// <summary>
        /// 目标温度
        /// </summary>
        [JsonProperty("targetTemp")]
        [LocalizedDisplayName(nameof(Resources.TemperatureTarget)), PropertyVisibility(nameof(TempCtlChecked))]
        public float TargetTemp { get => _TargetTemp; set { _TargetTemp = value; OnPropertyChanged(); } }
        private float _TargetTemp = 10.0f;
        /// <summary>
        /// 温度查询时间间隔
        /// </summary>
        [LocalizedDisplayName(nameof(Resources.TempQuaryInterval_S)), PropertyVisibility(nameof(TempCtlChecked))]
        public int TempSpanTime { get; set; } = 60;

        /// <summary>
        /// 传输速率
        /// </summary>
        [JsonProperty("usbTraffic")]
        [LocalizedDisplayName(nameof(Resources.usbTraffic))]
        public float UsbTraffic { get => _UsbTraffic; set { _UsbTraffic = value; OnPropertyChanged(); } }
        private float _UsbTraffic;

        /// <summary>
        /// 偏移
        /// </summary>
        [JsonProperty("offset")]
        [LocalizedDisplayName(nameof(Resources.offset))]
        public int Offset { get => _Offset; set { _Offset = value; OnPropertyChanged(); } }
        private int _Offset;

        /// <summary>
        /// 增益
        /// </summary>
        [JsonProperty("gain")]
        [LocalizedDisplayName(nameof(Resources.Gain))]
        public int Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }
        private int _Gain = 10;

        [JsonIgnore]
        [Browsable(true)]
        public Int32Rect ROI
        {
            get => new(PointX, PointY, Width, Height);
            set
            {
                PointX = value.X;
                PointY = value.Y;
                Width = value.Width;
                Height = value.Height;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// FindLightArea X
        /// </summary>
        [JsonProperty("ex")]
        [Browsable(false)]
        public int PointX { get => _PointX; set { _PointX = value; OnPropertyChanged(); OnPropertyChanged(nameof(ROI)); } }
        private int _PointX;
        /// <summary>
        /// FindLightArea Y
        /// </summary>
        [JsonProperty("ey")]
        [Browsable(false)]
        public int PointY { get => _PointY; set { _PointY = value; OnPropertyChanged(); OnPropertyChanged(nameof(ROI)); } }
        private int _PointY;
        /// <summary>
        /// FindLightArea W
        /// </summary>
        [JsonProperty("ew")]
        [Browsable(false)]
        public int Width { get => _Width; set { _Width = value; OnPropertyChanged(); OnPropertyChanged(nameof(ROI)); } }
        private int _Width;
        /// <summary>
        /// FindLightArea H
        /// </summary>
        [JsonProperty("eh")]
        [Browsable(false)]
        public int Height { get => _Height; set { _Height = value; OnPropertyChanged(); OnPropertyChanged(nameof(ROI)); } }
        private int _Height;


    }
}

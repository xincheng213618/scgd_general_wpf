﻿using Newtonsoft.Json;
using cvColorVision;
using ColorVision.Engine.Services.Devices.Camera.Video;
using System;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.Configs;
using System.Collections.ObjectModel;
using System.Windows;
using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.Camera.Configs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigCamera : DeviceServiceConfig, IFileServerCfg
    {
        public ConfigCamera()
        {
            AddROIParamsCommand = new RelayCommand(a => AddROIParams());
            DeleteROIParamsCommand = new RelayCommand(a => DeleteROIParams(a));
        }
        public string? CameraCode { get => _CameraCode; set { _CameraCode = value; NotifyPropertyChanged();  } }
        private string? _CameraCode;

        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged();} }
        private string _CameraID;

        public CameraType CameraType { get => _CameraType; set { if (_CameraType == value) return; _CameraType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); UpdateCameraModeAndIBM(value); } }
        private CameraType _CameraType;

        public CameraMode CameraMode { get => _CameraMode; set { if (_CameraMode == value) return; _CameraMode = value; NotifyPropertyChanged(); CameraType = GetCameraType(_CameraMode, _CameraModel); } }
        private CameraMode _CameraMode;

        public CameraModel CameraModel { get => _CameraModel; set { if (_CameraModel == value) return; _CameraModel = value; NotifyPropertyChanged(); CameraType = GetCameraType(_CameraMode, _CameraModel); } }
        private CameraModel _CameraModel;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); NotifyPropertyChanged(nameof(IsChannelThree)); } }
        private ImageChannel _Channel;

        public CameraVideoConfig VideoConfig { get; set; } = new CameraVideoConfig();

        public int AvgCount { get => _AvgCount; set { _AvgCount = value; NotifyPropertyChanged(); } }
        private int _AvgCount = 1;

        public bool UsingFileCaching { get => _UsingFileCaching; set { _UsingFileCaching = value; NotifyPropertyChanged(); } }
        private bool _UsingFileCaching;

        public bool IsCVCIEFileSave { get => _IsCVCIEFileSave; set { _IsCVCIEFileSave = value; NotifyPropertyChanged(); } }
        private bool _IsCVCIEFileSave = true;

        public int Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private int _Gain = 10;

        public double ScaleFactor { get => _ScaleFactor;set { _ScaleFactor = value; NotifyPropertyChanged(); } }
        private double _ScaleFactor = 1.0;

        public string  ScaleFactorUnit { get => _ScaleFactorUnit; set { _ScaleFactorUnit = value; NotifyPropertyChanged(); } }
        private string _ScaleFactorUnit = "Px";

        [JsonIgnore]
        public bool IsExpThree
        {
            get => TakeImageMode != TakeImageMode.Live && (CameraType == CameraType.CV_Q || CameraType == CameraType.CV_MIL_CL);
            set => NotifyPropertyChanged();
        }
        [JsonIgnore]
        public bool IsChannelThree
        {
            get => Channel == ImageChannel.Three;
            set => NotifyPropertyChanged();
        }

        public bool IsAutoExpose { get => _IsAutoExpose; set { _IsAutoExpose =value; NotifyPropertyChanged(); } }
        private bool _IsAutoExpose;

        public float ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeLog)); } }
        private float _ExpTime = 10;

        public double ExpTimeLog { get => Math.Log(ExpTime); set { ExpTime = (int)Math.Pow(Math.E, value); } }

        public double ExpTimeMax { get => _ExpTimeMax; set { _ExpTimeMax = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeMaxLog)); } }
        private double _ExpTimeMax = 60000;

        public double ExpTimeMaxLog { get => Math.Log(ExpTimeMax); set { ExpTimeMax = (int)Math.Pow(Math.E, value); } }

        public double ExpTimeMin { get => _ExpTimeMin; set { _ExpTimeMin = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeMinLog)); } }
        private double _ExpTimeMin = 1;

        public double ExpTimeMinLog { get => Math.Log(ExpTimeMin); set { ExpTimeMin = (int)Math.Pow(Math.E, value); } }

        public float ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeRLog)); } }
        private float _ExpTimeR = 10;

        public double ExpTimeRLog { get => Math.Log(ExpTimeR); set { ExpTimeR = (int)Math.Pow(Math.E, value); } }

        public float ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeGLog)); } }
        private float _ExpTimeG = 10;
        public double ExpTimeGLog { get => Math.Log(ExpTimeG); set { ExpTimeG = (int)Math.Pow(Math.E, value); } }

        public float ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeBLog)); } }
        private float _ExpTimeB = 10;
        public double ExpTimeBLog { get => Math.Log(ExpTimeB); set { ExpTimeB = (int)Math.Pow(Math.E, value); } }


        public double Saturation { get => _Saturation; set { _Saturation = value; NotifyPropertyChanged(); } }
        private double _Saturation = -1;

        public double SaturationR { get => _SaturationR; set { _SaturationR = value; NotifyPropertyChanged(); } }
        private double _SaturationR = -1;

        public double SaturationG { get => _SaturationG; set { _SaturationG = value; NotifyPropertyChanged(); } }
        private double _SaturationG = -1;

        public double SaturationB { get => _SaturationB; set { _SaturationB = value; NotifyPropertyChanged(); } }
        private double _SaturationB = -1;

        public CFWPORT CFW { get => _CFW; set { _CFW = value; NotifyPropertyChanged(); } }
        private CFWPORT _CFW = new CFWPORT();

        public MotorConfig MotorConfig { get => _MotorConfig; set { _MotorConfig = value; NotifyPropertyChanged(); } }
        private MotorConfig _MotorConfig = new MotorConfig();

        public AutoFocusConfig AutoFocusConfig { get; set; } = new AutoFocusConfig();

        public PhyExpTimeCfg ExpTimeCfg { get; set; } = new PhyExpTimeCfg();

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen = true;


        public static CameraType GetCameraType(CameraMode camMode, CameraModel camModel)
        {
            if (camMode == CameraMode.CV_MODE && camModel == CameraModel.QHY_USB)
                return CameraType.CV_Q;
            if (camMode == CameraMode.LV_MODE && camModel == CameraModel.QHY_USB)
                return CameraType.LV_Q;
            if (camMode == CameraMode.BV_MODE && camModel == CameraModel.QHY_USB)
                return CameraType.BV_Q;
            if (camMode == CameraMode.LV_MODE && camModel == CameraModel.MIL_CL_CARD)
                return CameraType.MIL_CL;
            if (camMode == CameraMode.LV_MODE && camModel == CameraModel.MIL_CXP_CARD)
                return CameraType.MIL_CXP;
            if (camMode == CameraMode.BV_MODE && camModel == CameraModel.HK_USB)
                return CameraType.BV_H;
            if (camMode == CameraMode.LV_MODE && camModel == CameraModel.HK_USB)
                return CameraType.LV_H;
            if (camMode == CameraMode.LV_MODE && camModel == CameraModel.HK_CARD)
                return CameraType.HK_CXP;
            if (camMode == CameraMode.LV_MODE && camModel == CameraModel.MIL_CL_CARD)
                return CameraType.LV_MIL_CL;
            if (camMode == CameraMode.CV_MODE && camModel == CameraModel.MIL_CL_CARD)
                return CameraType.CV_MIL_CL;
            return CameraType.CameraType_Total; // Default case if no match found
        }

        public bool UpdateCameraModeAndIBM(CameraType eCamType)
        {
            switch (eCamType)
            {
                case CameraType.CV_Q:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.LV_Q:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.BV_Q:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.MIL_CL:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.MIL_CL_CARD;
                    break;
                case CameraType.MIL_CXP:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.MIL_CXP_CARD;
                    break;
                case CameraType.BV_H:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.HK_USB;
                    break;
                case CameraType.LV_H:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.HK_USB;
                    break;
                case CameraType.HK_CXP:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.HK_CARD;
                    break;
                case CameraType.LV_MIL_CL:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.MIL_CL_CARD;
                    break;
                case CameraType.CV_MIL_CL:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.MIL_CL_CARD;
                    break;
                case CameraType.BV_MIL_CXP:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.MIL_CXP_CARD;
                    break;
                case CameraType.BV_HK_CARD:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.LV_HK_CARD:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.HK_CARD;
                    break;
                case CameraType.CV_HK_CARD:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.HK_CARD;
                    break;
                case CameraType.CV_HK_USB:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.HK_USB;
                    break;
                case CameraType.CameraType_Total:
                    // Process CameraType_Total case if needed
                    break;
                default:
                    // Process default case if needed
                    break;
            }
            return false;
        }

        public ZBDebayer ZBDebayer { get => _ZBDebayer; set { _ZBDebayer = value; NotifyPropertyChanged(); } }
        private ZBDebayer _ZBDebayer = new ZBDebayer();


        public RelayCommand AddROIParamsCommand { get; set; }
        public RelayCommand DeleteROIParamsCommand { get; set; }

        public void AddROIParams()
        {
            ROIParams.Add(new Int32RectViewModel(0, 0, 100, 100));
        }
        public void DeleteROIParams(Object obj)
        {
            if (obj is Int32RectViewModel  viewModel)
            ROIParams.Remove(viewModel);
        }

        public ObservableCollection<Int32RectViewModel> ROIParams { get; set; } = new ObservableCollection<Int32RectViewModel>();
    }

    public class Int32RectViewModel : ViewModelBase
    {
        public Int32RectViewModel(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Width { get => _Width; set { _Width = value; NotifyPropertyChanged(); } }
        private int _Width;
        public int Height { get => _Height; set { _Height = value; NotifyPropertyChanged(); } }
        private int _Height;
        public int X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private int _X;
        public int Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private int _Y;

        public Int32Rect ToInt32Rect()=> new Int32Rect(X, Y, Width, Height);
    }

}
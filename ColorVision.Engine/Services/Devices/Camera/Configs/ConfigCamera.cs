﻿using Newtonsoft.Json;
using cvColorVision;
using ColorVision.Engine.Services.Devices.Camera.Video;
using System;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras;
using System.Linq;
using ColorVision.Engine.Services.Configs;

namespace ColorVision.Engine.Services.Devices.Camera.Configs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigCamera : DeviceServiceConfig
    {
        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(CameraCode)); } }
        private string _CameraID;

        [JsonIgnore]
       public string? CameraCode
        {
            get
            {
                if (string.IsNullOrWhiteSpace(CameraID)) return null;
                var PhyCamera = PhyCameraManager.GetInstance().PhyCameras.First(a => a.Name == CameraID);
                if (PhyCamera != null)
                    return PhyCamera.SysResourceModel.Code;
                return null;

            }
        }

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

        public int ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeLog)); } }
        private int _ExpTime = 10;

        public double ExpTimeLog { get => Math.Log(ExpTime); set { ExpTime = (int)Math.Pow(Math.E, value); } }

        public int ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeRLog)); } }
        private int _ExpTimeR = 10;

        public double ExpTimeRLog { get => Math.Log(ExpTimeR); set { ExpTimeR = (int)Math.Pow(Math.E, value); } }

        public int ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeGLog)); } }
        private int _ExpTimeG = 10;
        public double ExpTimeGLog { get => Math.Log(ExpTimeG); set { ExpTimeG = (int)Math.Pow(Math.E, value); } }

        public int ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeBLog)); } }
        private int _ExpTimeB = 10;
        public double ExpTimeBLog { get => Math.Log(ExpTimeB); set { ExpTimeB = (int)Math.Pow(Math.E, value); } }


        public double Saturation { get => _Saturation; set { _Saturation = value; NotifyPropertyChanged(); } }
        private double _Saturation = -1;

        public double SaturationR { get => _SaturationR; set { _SaturationR = value; NotifyPropertyChanged(); } }
        private double _SaturationR = -1;

        public double SaturationG { get => _SaturationG; set { _SaturationG = value; NotifyPropertyChanged(); } }
        private double _SaturationG = -1;

        public double SaturationB { get => _SaturationB; set { _SaturationB = value; NotifyPropertyChanged(); } }
        private double _SaturationB = -1;

        public CFWPORT CFW { get; set; } = new CFWPORT();

        public bool IsHaveMotor { get => _IsHaveMotor; set { _IsHaveMotor = value; NotifyPropertyChanged(); } }
        private bool _IsHaveMotor;

        public MotorConfig MotorConfig { get; set; } = new MotorConfig();
          
        public PhyExpTimeCfg ExpTimeCfg { get; set; } = new PhyExpTimeCfg();

        public PhyCameraCfg CameraCfg { get; set; } = new PhyCameraCfg();

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
                    // Handle CameraType_Total case if needed
                    break;
                default:
                    // Handle default case if needed
                    break;
            }
            return false;
        }

    }
}
using ColorVision.Engine.Templates;
using ColorVision.Engine.Utilities;
using System.Collections.Generic;
using System.ComponentModel;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam
{
    public class CameraRunParam : ParamModBase
    {
        public CameraRunParam()
        {

        }
        public CameraRunParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        [Category("Gain")]
        [LocalizedDisplayName(typeof(Resources), "Gain"), LocalizedDescription(typeof(Resources), "Gain")]
        public float Gain { get => GetValue(_Gain); set { SetProperty(ref _Gain, value); } }
        private float _Gain;


        [Category("CameraExp")]
        [LocalizedDisplayName(typeof(Resources), "Exposure"), LocalizedDescription(typeof(Resources), "Exposure")]
        public float ExpTime { get => GetValue(_ExpTime); set { SetProperty(ref _ExpTime, value); } }
        private float _ExpTime = 10;


        [Category("CameraExp")]
        [LocalizedDisplayName(typeof(Resources), "ExposurR"), LocalizedDescription(typeof(Resources), "ExposureR")]
        public float ExpTimeR { get => GetValue(_ExpTimeR); set { SetProperty(ref _ExpTimeR, value); } }
        private float _ExpTimeR = 10;


        [Category("CameraExp")]
        [LocalizedDisplayName(typeof(Resources), "ExposureG"), LocalizedDescription(typeof(Resources), "ExposureG")]
        public float ExpTimeG { get => GetValue(_ExpTimeG); set { SetProperty(ref _ExpTimeG, value); } }
        private float _ExpTimeG = 10;


        [Category("CameraExp")]
        [LocalizedDisplayName(typeof(Resources), "ExposureB"),LocalizedDescription(typeof(Resources), "ExposureB")]
        public float ExpTimeB { get => GetValue(_ExpTimeB); set { SetProperty(ref _ExpTimeB, value); } }
        private float _ExpTimeB = 10;


        [Category("Focus")]
        [LocalizedDisplayName(typeof(Resources), "EnableFocus"), LocalizedDescription(typeof(Resources), "EnableFocus")]
        public bool EnableFocus { get => GetValue(_EnableFocus); set { SetProperty(ref _EnableFocus, value); } }
        private bool _EnableFocus;

        [Category("Camera")]
        [LocalizedDisplayName(typeof(Resources), "AverageTimes"),LocalizedDescription(typeof(Resources), "AverageTimes")]
        public int AvgCount { get => GetValue(_AvgCount); set { SetProperty(ref _AvgCount, value); } }
        private int _AvgCount = 1;


        [Category("Focus")]
        [LocalizedDisplayName(typeof(Resources), "Focus"), LocalizedDescription(typeof(Resources), "Focus")]
        public int Focus { get => GetValue(_Focus); set { SetProperty(ref _Focus, value); } }
        private int _Focus = -1;

        [Category("Camera")]
        [LocalizedDisplayName(typeof(Resources), "ApertureF"), LocalizedDescription(typeof(Resources), "ApertureF")]
        public int Aperture { get => GetValue(_Aperture); set { SetProperty(ref _Aperture, value); } }
        private int _Aperture = -1;

    }

}

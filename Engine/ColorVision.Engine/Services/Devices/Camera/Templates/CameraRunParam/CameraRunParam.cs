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
        [LocalizedDisplayName(nameof(Resources.Gain)), LocalizedDescription(nameof(Resources.Gain))]
        public float Gain { get => GetValue(_Gain); set { SetProperty(ref _Gain, value); } }
        private float _Gain;


        [Category("CameraExp")]
        [LocalizedDisplayName(nameof(Resources.Exposure)), LocalizedDescription(nameof(Resources.Exposure))]
        public float ExpTime { get => GetValue(_ExpTime); set { SetProperty(ref _ExpTime, value); } }
        private float _ExpTime = 10;


        [Category("CameraExp")]
        [LocalizedDescription(nameof(Resources.ExposureR))]
        public float ExpTimeR { get => GetValue(_ExpTimeR); set { SetProperty(ref _ExpTimeR, value); } }
        private float _ExpTimeR = 10;


        [Category("CameraExp")]
        [LocalizedDisplayName(nameof(Resources.ExposureG)), LocalizedDescription(nameof(Resources.ExposureG))]
        public float ExpTimeG { get => GetValue(_ExpTimeG); set { SetProperty(ref _ExpTimeG, value); } }
        private float _ExpTimeG = 10;


        [Category("CameraExp")]
        [LocalizedDisplayName(nameof(Resources.ExposureB)),LocalizedDescription(nameof(Resources.ExposureB))]
        public float ExpTimeB { get => GetValue(_ExpTimeB); set { SetProperty(ref _ExpTimeB, value); } }
        private float _ExpTimeB = 10;


        [Category("Focus")]
        [LocalizedDisplayName(nameof(Resources.EnableFocus)), LocalizedDescription(nameof(Resources.EnableFocus))]
        public bool EnableFocus { get => GetValue(_EnableFocus); set { SetProperty(ref _EnableFocus, value); } }
        private bool _EnableFocus;

        [Category("Camera")]
        [LocalizedDisplayName(nameof(Resources.AverageTimes)),LocalizedDescription(nameof(Resources.AverageTimes))]
        public int AvgCount { get => GetValue(_AvgCount); set { SetProperty(ref _AvgCount, value); } }
        private int _AvgCount = 1;


        [Category("Focus")]
        [LocalizedDisplayName(nameof(Resources.Focus)), LocalizedDescription(nameof(Resources.Focus))]
        public int Focus { get => GetValue(_Focus); set { SetProperty(ref _Focus, value); } }
        private int _Focus = -1;

        [Category("Camera")]
        [LocalizedDisplayName(nameof(Resources.ApertureF)), LocalizedDescription(nameof(Resources.ApertureF))]
        public int Aperture { get => GetValue(_Aperture); set { SetProperty(ref _Aperture, value); } }
        private int _Aperture = -1;

    }

}

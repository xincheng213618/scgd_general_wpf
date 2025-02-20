using System.Collections.Generic;
using System.ComponentModel;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure
{
    public class CameraExposureParam : ParamModBase
    {
        public CameraExposureParam() : base()
        {

        }
        public CameraExposureParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }

        [Category("Gain"),DisplayName("增益"), Description("增益")]
        public float Gain { get => GetValue(_Gain); set { SetProperty(ref _Gain, value); } }
        private float _Gain;


        [Category("CameraExp"), Description("ExpTime")]
        public float ExpTime { get => GetValue(_ExpTime); set { SetProperty(ref _ExpTime, value); } }
        private float _ExpTime = 10;


        [Category("CameraExp"), Description("ExpTime1")]
        public float ExpTimeR { get => GetValue(_ExpTimeR); set { SetProperty(ref _ExpTimeR, value); } }
        private float _ExpTimeR = 10;


        [Category("CameraExp"), Description("ExpTime2")]
        public float ExpTimeG { get => GetValue(_ExpTimeG); set { SetProperty(ref _ExpTimeG, value); } }
        private float _ExpTimeG = 10;


        [Category("CameraExp") ,Description("ExpTime3")]
        public float ExpTimeB { get => GetValue(_ExpTimeB); set { SetProperty(ref _ExpTimeB, value); } }
        private float _ExpTimeB = 10;


        [Category("Focus"), Description("焦距")]
        public bool EnableFocus { get => GetValue(_EnableFocus); set { SetProperty(ref _EnableFocus, value); } }
        private bool _EnableFocus;

        [Category("Camera"), Description("平均次数")]
        public int AvgCount { get => GetValue(_AvgCount); set { SetProperty(ref _AvgCount, value); } }
        private int _AvgCount = 1;


        [Category("Focus"), Description("焦距")]
        public int Focus { get => GetValue(_Focus); set { SetProperty(ref _Focus, value); } }
        private int _Focus = -1;

        [Category("Camera"), Description("光圈")]
        public int Aperture { get => GetValue(_Aperture); set { SetProperty(ref _Aperture, value); } }
        private int _Aperture = -1;

    }

}

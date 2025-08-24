using ColorVision.Engine.Templates.POI.POIGenCali;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.POI.POIRevise
{

    public class PoiReviseParam : ParamModBase
    {

        public PoiReviseParam()
        {
        }

        public PoiReviseParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }
        [Category("PoiReviseParam"), Description("M")]
        public float M { get => GetValue(_M); set { SetProperty(ref _M, value); } }
        private float _M = 0.01f;

        [Category("PoiReviseParam"), Description("N")]
        public float N { get => GetValue(_N); set { SetProperty(ref _N, value); } }
        private float _N = 0.01f;

        [Category("PoiReviseParam"), Description("P")]
        public float P { get => GetValue(_P); set { SetProperty(ref _P, value); } }
        private float _P = 0.01f;

        [Category("PoiReviseParam"), Description("修正方式")]
        public GenCalibrationType GenCalibrationType { get => GetValue(_GenCalibrationType); set { SetProperty(ref _GenCalibrationType, value); OnPropertyChanged(); } }
        private GenCalibrationType _GenCalibrationType = GenCalibrationType.BrightnessAndChroma;
    }
}

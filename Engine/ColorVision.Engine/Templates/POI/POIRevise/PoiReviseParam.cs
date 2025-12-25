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
        [Category("PoiReviseParam"), Description("Offx：色度 x 的偏移修正（被减去）。用途：校正测量仪器的 x 偏差或人为想要移动的色度量。")]
        public float M { get => GetValue(_M); set { SetProperty(ref _M, value); } }
        private float _M = 0.01f;

        [Category("PoiReviseParam"), Description("Offy：色度 y 的偏移修正（被减去）。")]
        public float N { get => GetValue(_N); set { SetProperty(ref _N, value); } }
        private float _N = 0.01f;

        [Category("PoiReviseParam"), Description("OffLv：亮度（Y）的乘法缩放因子，用来调整/校正亮度等级（例如补偿仪器灵敏度或场景亮度）。注意不是加法偏移，而是乘法比例。")]
        public float P { get => GetValue(_P); set { SetProperty(ref _P, value); } }
        private float _P = 0.01f;

        [Category("PoiReviseParam"), Description("修正方式")]
        public GenCalibrationType GenCalibrationType { get => GetValue(_GenCalibrationType); set { SetProperty(ref _GenCalibrationType, value); OnPropertyChanged(); } }
        private GenCalibrationType _GenCalibrationType = GenCalibrationType.BrightnessAndChroma;
    }
}

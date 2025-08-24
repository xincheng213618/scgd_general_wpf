using System.Collections.Generic;

namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class PoiFilterParam : ParamModBase
    {
        public PoiFilterParam()
        {

        }

        public PoiFilterParam(ModMasterModel modMaster, List<ModDetailModel> aoiDetail) : base(modMaster, aoiDetail)
        {
            ModMaster = modMaster;
        }

        public bool NoAreaEnable { get => GetValue(_NoAreaEnable); set { SetProperty(ref _NoAreaEnable, value); OnPropertyChanged(); if (value) { Enable = false; XYZEnable = false; } } }
        private bool _NoAreaEnable;
        public bool Enable { get => GetValue(_Enable); set { SetProperty(ref _Enable, value); OnPropertyChanged(); if (value) { NoAreaEnable = false; XYZEnable = false; } } }
        private bool _Enable;

        public bool XYZEnable { get => GetValue(_XYZEnable); set { SetProperty(ref _XYZEnable, value); OnPropertyChanged(); if (value) { NoAreaEnable = false; Enable = false; } } }
        private bool _XYZEnable;
        public int XYZType { get => GetValue(_XYZType); set { SetProperty(ref _XYZType, value); OnPropertyChanged(); } }
        private int _XYZType;

        public bool ThresholdUsePercent { get => GetValue(_ThresholdUsePercent); set { SetProperty(ref _ThresholdUsePercent, value); OnPropertyChanged(); if (Threshold >= 1) Threshold = 1; if (Threshold <= 0) Threshold = 0; } }
        private bool _ThresholdUsePercent;
        public float Threshold { get => GetValue(_Threshold); set { if (ThresholdUsePercent && value >= 1) value = 1; if (ThresholdUsePercent && value <= 0) value = 0; SetProperty(ref _Threshold, value); OnPropertyChanged(); } }
        private float _Threshold = 50;

        public float MaxPercent { get => GetValue(_MaxPercent); set { if (value >= 1) value = 1; if (value <= 0) value = 0; SetProperty(ref _MaxPercent, value); OnPropertyChanged(); } }
        private float _MaxPercent = 0.2f;
    }


}

﻿using ColorVision.Engine.Services.Devices.Algorithm.Templates.FOV;
using ColorVision.Engine.Templates.POI;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.POI.POIFilters
{
    public class POIFilterParam : ParamBase
    {
        public POIFilterParam()
        {

        }

        public POIFilterParam(ModMasterModel modMaster, List<ModDetailModel> aoiDetail) : base(modMaster.Id, modMaster.Name ?? string.Empty, aoiDetail)
        {

        }

        public bool NoAreaEnable { get => GetValue(_NoAreaEnable); set { SetProperty(ref _NoAreaEnable, value); NotifyPropertyChanged(); if (value) { Enable = false; XYZEnable = false; } } }
        private bool _NoAreaEnable;
        public bool Enable { get => GetValue(_Enable); set { SetProperty(ref _Enable, value); NotifyPropertyChanged(); if (value) { NoAreaEnable = false; XYZEnable = false; } } }
        private bool _Enable;

        public bool XYZEnable { get => GetValue(_XYZEnable); set { SetProperty(ref _XYZEnable, value); NotifyPropertyChanged(); if (value) { NoAreaEnable = false; Enable = false; } } }
        private bool _XYZEnable;
        public XYZType XYZType { get => GetValue(_XYZType); set { SetProperty(ref _XYZType, value); NotifyPropertyChanged(); } }
        private XYZType _XYZType;

        public float Threshold { get => GetValue(_Threshold); set { SetProperty(ref _Threshold, value); NotifyPropertyChanged(); } }
        private float _Threshold = 50;

    }
}
#pragma warning disable CA1707,IDE1006

using ColorVision.Engine.Templates;
using ColorVision.Engine.Utilities;
using System.Collections.Generic;
using System.ComponentModel;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam
{

    public class AutoExpTimeParam : ParamModBase
    {
        public AutoExpTimeParam() { }
        public AutoExpTimeParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }
        [LocalizedDisplayName(typeof(Resources), "IsEnable"), LocalizedDescription(typeof(Resources), "IsEnable")]
        public bool autoExpFlag { get => GetValue(_autoExpFlag); set { SetProperty(ref _autoExpFlag, value); } }
        private bool _autoExpFlag = true;

        [LocalizedDisplayName(typeof(Resources), "StartTimeMs")]
        public int autoExpTimeBegin { get => GetValue(_autoExpTimeBegin); set { SetProperty(ref _autoExpTimeBegin, value); } }
        private int _autoExpTimeBegin = 10;

        [LocalizedDisplayName(typeof(Resources), "FrequencySyncHz")]
        public int autoExpSyncFreq { get => GetValue(_autoExpSyncFreq); set { SetProperty(ref _autoExpSyncFreq, value); } }
        private int _autoExpSyncFreq = -1;

        [LocalizedDisplayName(typeof(Resources), "SaturationPercent")]
        public int autoExpSaturation { get => GetValue(_autoExpSaturation); set { SetProperty(ref _autoExpSaturation, value); } }
        private int _autoExpSaturation = 70;

        [LocalizedDisplayName(typeof(Resources), "ADMax")]
        public int autoExpSatMaxAD { get => GetValue(_autoExpSatMaxAD); set { SetProperty(ref _autoExpSatMaxAD, value); } }
        private int _autoExpSatMaxAD = 65000;

        [LocalizedDisplayName(typeof(Resources), "MaxPercent")]
        public double autoExpMaxPecentage { get => GetValue(_autoExpMaxPecentage); set { SetProperty(ref _autoExpMaxPecentage, value); } }
        private double _autoExpMaxPecentage = 0.01;

        [LocalizedDisplayName(typeof(Resources), "SaturationDifference")]
        public int autoExpSatDev { get => GetValue(_autoExpSatDev); set { SetProperty(ref _autoExpSatDev, value); } }
        private int _autoExpSatDev = 20;

        [LocalizedDisplayName(typeof(Resources), "MaxExposureTimeMs")]
        public double maxExpTime { get => GetValue(_maxExpTime); set { SetProperty(ref _maxExpTime, value); } }
        private double _maxExpTime = 60000;

        [LocalizedDisplayName(typeof(Resources), "MinExposureTimeMs")]
        public double minExpTime { get => GetValue(_minExpTime); set { SetProperty(ref _minExpTime, value); } }
        private double _minExpTime = 0.2;

        [LocalizedDisplayName(typeof(Resources), "BurstThresholdMs")]
        public int burstThreshold { get => GetValue(_burstThreshold); set { SetProperty(ref _burstThreshold, value); } }
        private int _burstThreshold = 200;

    }
}

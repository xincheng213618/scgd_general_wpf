#pragma warning disable CA1707,IDE1006

using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam
{

    public class AutoExpTimeParam : ParamModBase
    {
        public AutoExpTimeParam() { }
        public AutoExpTimeParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }
        [DisplayName("是否启用")]
        public bool autoExpFlag { get => GetValue(_autoExpFlag); set { SetProperty(ref _autoExpFlag, value); } }
        private bool _autoExpFlag = true;

        [DisplayName("开始时间(ms)")]
        public int autoExpTimeBegin { get => GetValue(_autoExpTimeBegin); set { SetProperty(ref _autoExpTimeBegin, value); } }
        private int _autoExpTimeBegin = 10;

        [DisplayName("频率同步(hz)")]
        public int autoExpSyncFreq { get => GetValue(_autoExpSyncFreq); set { SetProperty(ref _autoExpSyncFreq, value); } }
        private int _autoExpSyncFreq = -1;

        [DisplayName("饱和度(%)")]
        public int autoExpSaturation { get => GetValue(_autoExpSaturation); set { SetProperty(ref _autoExpSaturation, value); } }
        private int _autoExpSaturation = 70;

        [DisplayName("AD最大值")]
        public int autoExpSatMaxAD { get => GetValue(_autoExpSatMaxAD); set { SetProperty(ref _autoExpSatMaxAD, value); } }
        private int _autoExpSatMaxAD = 65000;

        [DisplayName("最大值百分比")]
        public double autoExpMaxPecentage { get => GetValue(_autoExpMaxPecentage); set { SetProperty(ref _autoExpMaxPecentage, value); } }
        private double _autoExpMaxPecentage = 0.01;

        [DisplayName("饱和度差值")]
        public int autoExpSatDev { get => GetValue(_autoExpSatDev); set { SetProperty(ref _autoExpSatDev, value); } }
        private int _autoExpSatDev = 20;

        [DisplayName("最大曝光时间(ms)")]
        public double maxExpTime { get => GetValue(_maxExpTime); set { SetProperty(ref _maxExpTime, value); } }
        private double _maxExpTime = 60000;

        [DisplayName("最小曝光时间(ms)")]
        public double minExpTime { get => GetValue(_minExpTime); set { SetProperty(ref _minExpTime, value); } }
        private double _minExpTime = 0.2;

        [DisplayName("burst阈值(ms)")]
        public int burstThreshold { get => GetValue(_burstThreshold); set { SetProperty(ref _burstThreshold, value); } }
        private int _burstThreshold = 200;

    }
}

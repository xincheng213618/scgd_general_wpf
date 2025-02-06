using cvColorVision;
using System.ComponentModel;
using ColorVision.Engine.Templates;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus
{
    public class AutoFocusParam : ParamModBase
    {
        public AutoFocusParam() { }
        public AutoFocusParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [DisplayName("评价函数类型")]
        public EvaFunc EvaFunc { get => GetValue(_EvaFunc); set { SetProperty(ref _EvaFunc, value); } }
        private EvaFunc _EvaFunc = EvaFunc.Variance;

        [DisplayName("步径摆动范围"),Description("第二次的步径 实际第二次步径为 forwardparam * ")]
        public double Forwardparam { get => GetValue(_Forwardparam); set { SetProperty(ref _Forwardparam, value); } }
        private double _Forwardparam = 2000;


        [DisplayName("步径每次缩减系数"), Description(" 第二次的步径系数 ")]
        public double Curtailparam { get => GetValue(_Curtailparam); set { SetProperty(ref _Curtailparam, value); } }
        private double _Curtailparam = 0.3;

        [DisplayName("目前使用步径"), Description(" 第一次的步径 ")]
        public int CurStep { get => GetValue(_CurStep); set { SetProperty(ref _CurStep, value); } }
        private int _CurStep = 5000;
        [DisplayName("停止步径"), Description("已弃用")]
        public int StopStep { get => GetValue(_StopStep); set { SetProperty(ref _StopStep, value); } }
        private int _StopStep = 200;

        [DisplayName("电机移动区间下限")]
        public int MinPosition { get => GetValue(_MinPosition); set { SetProperty(ref _MinPosition, value); } }
        private int _MinPosition = 80000;

        [DisplayName("电机移动区间上限")]
        public int MaxPosition { get => GetValue(_MaxPosition); set { SetProperty(ref _MaxPosition, value); } }
        private int _MaxPosition = 180000;

        [DisplayName("最低评价值")]
        public double MinValue { get => GetValue(_MinValue); set { SetProperty(ref _MinValue, value); } }
        private double _MinValue;

        [DisplayName("超时时间")]
        public int nTimeout { get => GetValue(_nTimeout); set { SetProperty(ref _nTimeout, value); } }
        private int _nTimeout = 30000;
    }
}
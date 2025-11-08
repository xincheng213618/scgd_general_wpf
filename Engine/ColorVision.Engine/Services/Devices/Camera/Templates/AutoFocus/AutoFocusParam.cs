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

        [DisplayName("EvaluationFunctionType")]
        public EvaFunc EvaFunc { get => GetValue(_EvaFunc); set { SetProperty(ref _EvaFunc, value); } }
        private EvaFunc _EvaFunc = EvaFunc.Variance;


        [DisplayName("StepSwingRange"),Description("StepSwingRangeDiscription")]
        public double Forwardparam { get => GetValue(_Forwardparam); set { SetProperty(ref _Forwardparam, value); } }
        private double _Forwardparam = 2000;


        [DisplayName("StepReductionFactor"), Description(" SecondStepFactor ")]
        public double Curtailparam { get => GetValue(_Curtailparam); set { SetProperty(ref _Curtailparam, value); } }
        private double _Curtailparam = 0.3;

        [DisplayName("CurrentStepSize"), Description(" InitialStepSize ")]
        public int CurStep { get => GetValue(_CurStep); set { SetProperty(ref _CurStep, value); } }
        private int _CurStep = 5000;
        [DisplayName("StopStepSize"), Description("Deprecated")]
        public int StopStep { get => GetValue(_StopStep); set { SetProperty(ref _StopStep, value); } }
        private int _StopStep = 200;

        [DisplayName("MotorMoveRangeLowerLimit")]
        public int MinPosition { get => GetValue(_MinPosition); set { SetProperty(ref _MinPosition, value); } }
        private int _MinPosition = 80000;

        [DisplayName("MotorMoveRangeupperLimit")]
        public int MaxPosition { get => GetValue(_MaxPosition); set { SetProperty(ref _MaxPosition, value); } }
        private int _MaxPosition = 180000;

        [DisplayName("MinEvaluationValue")]
        public double MinValue { get => GetValue(_MinValue); set { SetProperty(ref _MinValue, value); } }
        private double _MinValue;

        [DisplayName("TimeoutDuration")]
        public int nTimeout { get => GetValue(_nTimeout); set { SetProperty(ref _nTimeout, value); } }
        private int _nTimeout = 30000;

    }
}
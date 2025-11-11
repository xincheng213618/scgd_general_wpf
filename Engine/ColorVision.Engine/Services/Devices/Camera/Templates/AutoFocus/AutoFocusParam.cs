using ColorVision.Engine.Templates;
using ColorVision.Engine.Utilities;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus
{
    public class AutoFocusParam : ParamModBase
    {
        public AutoFocusParam() { }
        public AutoFocusParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [LocalizedDisplayName(typeof(Resources), "EvaluationFunctionType"), LocalizedDescription(typeof(Resources), "EvaluationFunctionType")]
        public EvaFunc EvaFunc { get => GetValue(_EvaFunc); set { SetProperty(ref _EvaFunc, value); } }
        private EvaFunc _EvaFunc = EvaFunc.Variance;


        [LocalizedDisplayName(typeof(Resources), "StepSwingRange"), LocalizedDescription(typeof(Resources), "StepSwingRangeDiscription")]
        public double Forwardparam { get => GetValue(_Forwardparam); set { SetProperty(ref _Forwardparam, value); } }
        private double _Forwardparam = 2000;


        [LocalizedDisplayName(typeof(Resources), "StepReductionFactor"), LocalizedDescription(typeof(Resources), "SecondStepFactor")]
        public double Curtailparam { get => GetValue(_Curtailparam); set { SetProperty(ref _Curtailparam, value); } }
        private double _Curtailparam = 0.3;

        [LocalizedDisplayName(typeof(Resources), "CurrentStepSize"), LocalizedDescription(typeof(Resources), "InitialStepSize")]
        public int CurStep { get => GetValue(_CurStep); set { SetProperty(ref _CurStep, value); } }
        private int _CurStep = 5000;
        [LocalizedDisplayName(typeof(Resources), "StopStepSize"), LocalizedDescription(typeof(Resources), "Deprecated")]
        public int StopStep { get => GetValue(_StopStep); set { SetProperty(ref _StopStep, value); } }
        private int _StopStep = 200;

        [LocalizedDisplayName(typeof(Resources), "MotorMoveRangeLowerLimit")]
        public int MinPosition { get => GetValue(_MinPosition); set { SetProperty(ref _MinPosition, value); } }
        private int _MinPosition = 80000;

        [LocalizedDisplayName(typeof(Resources), "MotorMoveRangeupperLimit")]
        public int MaxPosition { get => GetValue(_MaxPosition); set { SetProperty(ref _MaxPosition, value); } }
        private int _MaxPosition = 180000;

        [LocalizedDisplayName(typeof(Resources), "MinEvaluationValue")]
        public double MinValue { get => GetValue(_MinValue); set { SetProperty(ref _MinValue, value); } }
        private double _MinValue;

        [DisplayName("TimeoutDuration")]
        [LocalizedDisplayName(typeof(Resources), "TimeoutDuration")]
        public int nTimeout { get => GetValue(_nTimeout); set { SetProperty(ref _nTimeout, value); } }
        private int _nTimeout = 30000;

    }
}
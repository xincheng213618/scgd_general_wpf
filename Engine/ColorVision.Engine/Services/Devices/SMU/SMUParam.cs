#pragma warning disable CA1507
using ColorVision.Database;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Templates;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.SMU
{

    public class TemplateSMUParam : ITemplate<SMUParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<SMUParam>> Params { get; set; } = new ObservableCollection<TemplateModel<SMUParam>>();

        public TemplateSMUParam()
        {
            Title = "SMUParamConfig";
            Code = "SMU";
            TemplateDicId = 13;
            TemplateParams = Params;
        }
        public override IMysqlCommand? GetMysqlCommand() => new MysqlSMU();
    }



    public enum SMUMeasureType
    {
        VoltageSource = 0,
        CurrentSource = 1
    }


    public class SMUParam : ParamModBase
    {

        public SMUParam() { }

        [Description("Support A & B")]
        public SMUChannelType Channel
        {
            set { SetProperty(ref _Channel, value, "Channel"); }
            get => GetValue(_Channel, "Channel");
        }
        private SMUChannelType _Channel  = SMUChannelType.A;


        public SMUParam(ModMasterModel modMaster, List<ModDetailModel> sxDetail) : base(modMaster, sxDetail) { }
        [LocalizedDisplayName(nameof(Resources.StartMeasurementValue_V_mA)), LocalizedDescription(nameof(Resources.VoltageSourceUnit_V_CurrentSourceUnit_mA))]
        public double StartMeasureVal
        {
            set { SetProperty(ref _StartMeasureVal, value, "BeginValue"); }
            get => GetValue(_StartMeasureVal, "BeginValue");
        }
        [LocalizedDisplayName(nameof(Resources.EndMeasurementValue_V_mA)), LocalizedDescription(nameof(Resources.VoltageSourceUnit_V_CurrentSourceUnit_mA))]
        public double StopMeasureVal
        {
            set { SetProperty(ref _StopMeasureVal, value, "EndValue"); }
            get => GetValue(_StopMeasureVal, "EndValue");
        }
        [LocalizedDisplayName(nameof(Resources.PointCount)), LocalizedDescription(nameof(Resources.PointCount))]
        public int Number
        {
            set { SetProperty(ref _Number, value, "Points"); }
            get => GetValue(_Number, "Points");
        }
        [LocalizedDisplayName(nameof(Resources.LimitVal)), LocalizedDescription(nameof(Resources.SourceMeterProtectionLimit_VoltageUnitV_CurrentUnitmA))]
        public double LmtVal
        {
            set { SetProperty(ref _LmtVal, value, "LimitValue"); }
            get => GetValue(_LmtVal, "LimitValue");
        }
        [LocalizedDisplayName(nameof(Resources.IsVoltageSource)), LocalizedDescription(nameof(Resources.TrueIsVoltageSource_FalseIsCurrentSource))]
        public bool IsSourceV
        {
            set { SetProperty(ref _IsSourceV, value); }
            get => GetValue(_IsSourceV);
        }
        [LocalizedDisplayName(nameof(Resources.MeasureType)), LocalizedDescription(nameof(Resources.VoltageSourceCurrentSourceSelection))]
        public SMUMeasureType MeasureType
        {
            get => IsSourceV ? SMUMeasureType.VoltageSource : SMUMeasureType.CurrentSource;
            set => IsSourceV = value == SMUMeasureType.VoltageSource;
        }

        private double _StartMeasureVal;
        private double _StopMeasureVal;
        private double _LmtVal;
        private int _Number;
        private bool _IsSourceV;

        private bool _IsAutoRng = true;
        public bool IsAutoRng
        {
            set { SetProperty(ref _IsAutoRng, value); }
            get => GetValue(_IsAutoRng);
        }
        private double _SrcRng;
        public double SrcRng
        {
            set { SetProperty(ref _SrcRng, value); }
            get => GetValue(_SrcRng);
        }

        private double _LmtRng;
        public double LmtRng
        {
            set { SetProperty(ref _LmtRng, value); }
            get => GetValue(_LmtRng);
        }
    }
}

using ColorVision.Engine.Templates;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.DataLoad
{

    public class DataLoadParam : ParamBase
    {

        public DataLoadParam()
        {
        }

        public DataLoadParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {

        }
        [Category("DataLoadParam"), Description("设备Code")]
        public string? DeviceCode { get => GetValue(_DeviceCode); set { SetProperty(ref _DeviceCode, value); } }
        private string? _DeviceCode;

        [Category("DataLoadParam"), Description("结果类型")]
        public CVCommCore.CVResultType ResultType { get => GetValue(_ResultType); set { SetProperty(ref _ResultType, value); } }
        private CVCommCore.CVResultType _ResultType = CVCommCore.CVResultType.None;

        [Category("DataLoadParam"), Description("流水号")]
        public string? SerialNumber { get => GetValue(_SerialNumber); set { SetProperty(ref _SerialNumber, value); } }
        private string? _SerialNumber;

        [Category("DataLoadParam"), Description("ZIndex")]
        public int ZIndex { get => GetValue(_ZIndex); set { SetProperty(ref _ZIndex, value); } }
        private int _ZIndex;

    }
}

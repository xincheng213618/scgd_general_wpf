using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates
{
    public class ExportDataLoadParam : ExportTemplateBase
    {
        public override string OwnerGuid => "TemplateAlgorithm";
        public override string GuidId => "DataLoad";
        public override string Header => "数据加载模板设置";
        public override int Order => 0;
        public override ITemplate Template => new TemplateDataLoadParam();
    }

    public class TemplateDataLoadParam : ITemplate<DataLoadParam>, IITemplateLoad
    {
        public TemplateDataLoadParam()
        {
            Title = "数据加载算法设置";
            Code = "DataLoad";
            TemplateParams = DataLoadParam.Params;
        }
    }

    public class DataLoadParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<DataLoadParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DataLoadParam>>();

        public DataLoadParam()
        {
        }
        public DataLoadParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {

        }
        [Category("DataLoadParam"), Description("设备Code")]
        public string? DeviceCode { get => GetValue(_DeviceCode); set { SetProperty(ref _DeviceCode, value); } }
        private string? _DeviceCode;

        [Category("DataLoadParam"), Description("结果类型")]
        public  CVCommCore.CVResultType ResultType { get => GetValue(_ResultType); set { SetProperty(ref _ResultType, value); } }
        private CVCommCore.CVResultType _ResultType = CVCommCore.CVResultType.None;

        [Category("DataLoadParam"), Description("流水号")]
        public string? SerialNumber { get => GetValue(_SerialNumber); set { SetProperty(ref _SerialNumber, value); } }
        private string? _SerialNumber;

        [Category("DataLoadParam"), Description("ZIndex")]
        public int ZIndex { get => GetValue(_ZIndex); set { SetProperty(ref _ZIndex, value); } }
        private int _ZIndex;
        
    }
}

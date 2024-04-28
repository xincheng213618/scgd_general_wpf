using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Flow.Dao;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.UI;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Flow
{
    public class FlowPlugin : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "FlowParam";
        public int Index => 0;
        public string? Header => "流程模板设置(_F)";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new RelayCommand(a => {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(TemplateType.FlowParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }


    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamBase
    {
        public FlowParam()
        {

        }

        public FlowParam(ModMasterModel dbModel, List<ModFlowDetailModel> flowDetail) : base()
        {
            Id = dbModel.Id;
            Name = dbModel.Name ?? string.Empty;
            List<ModDetailModel> modDetailModels = new List<ModDetailModel>();
            foreach (var model in flowDetail)
            {
                ModDetailModel mod = new ModDetailModel() { Id = model.Id, Pid = model.Pid, IsDelete = model.IsDelete, IsEnable = model.IsEnable, Symbol = model.Symbol, SysPid = model.SysPid, ValueA = model.ValueA, ValueB = model.ValueB };
                modDetailModels.Add(mod);
                dataBase64 = model.Value ??string.Empty;
            }
            AddDetail(modDetailModels);
        }

        private string dataBase64;
        public string DataBase64 { get => dataBase64; set { dataBase64 = value; } }

        private const string propertyName = "filename";

        public string? ResId
        {
            set { SetProperty(ref _ResId, value?.ToString(), propertyName); }
            get => GetValue(_ResId, propertyName);
        }
        private string? _ResId;
    }
}

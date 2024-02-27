using ColorVision.Services.Dao;
using ColorVision.Services.Flow.Dao;
using ColorVision.Services.Templates;
using System.Collections.Generic;

namespace ColorVision.Services.Flow
{
    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamBase
    {
        public FlowParam()
        {
        }
        public FlowParam(ModMasterModel dbModel, List<ModDetailModel> flowDetail) : base(dbModel.Id, dbModel.Name ?? string.Empty, flowDetail)
        {

        }
        public FlowParam(ModMasterModel dbModel, List<ModFlowDetailModel> flowDetail) : base()
        {
            this.Id = dbModel.Id;
            this.Name = dbModel.Name ?? string.Empty;
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

        public const string propertyName = "filename";

        public string? ResId
        {
            set { SetProperty(ref _ResId, value?.ToString(), propertyName); }
            get => GetValue(_ResId, propertyName);
        }
        private string? _ResId;
    }
}

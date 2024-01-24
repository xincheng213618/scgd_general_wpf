using ColorVision.Services.Dao;
using ColorVision.Templates;
using System.Collections.Generic;

namespace ColorVision.Services.Flow.Templates
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

        private string dataBase64;
        public string DataBase64 { get => dataBase64; set { dataBase64 = value; } }

        public const string propertyName = "filename";

        public string? FileName
        {
            set { SetProperty(ref _FileName, value?.ToString(), propertyName); }
            get => GetValue(_FileName, propertyName);
        }
        private string? _FileName;
    }
}

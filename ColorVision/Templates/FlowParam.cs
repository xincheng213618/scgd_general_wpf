#pragma warning disable CS8603  

using ColorVision.MySql.DAO;
using System.Collections.Generic;

namespace ColorVision.Templates
{
    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamBase
    {
        public const string FileNameKey = "filename";
        public FlowParam() {
        }
        public FlowParam(ModMasterModel dbModel, List<ModDetailModel> flowDetail) : base(dbModel.Id, dbModel.Name??string.Empty, flowDetail)
        {
        }


        private string dataBase64;
        public string DataBase64 { get => dataBase64; set { dataBase64 = value; } }

        /// <summary>
        /// 流程文件名称
        /// </summary>
        public string? FileName
        {
            set { SetProperty(ref _FileName, value?.ToString(), FileNameKey); }
            get => GetValue(_FileName, FileNameKey);
        }
        private string? _FileName;
    }
}

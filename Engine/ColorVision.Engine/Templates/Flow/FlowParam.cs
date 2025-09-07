using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Flow
{
    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamModBase
    {
        public FlowParam() 
        {

        }

        public FlowParam(ModMasterModel dbModel, List<ModDetailModel> flowDetail) : base(dbModel, flowDetail)
        {
            _DataBase64 = flowDetail.Count >0 ? flowDetail[0].Value ?? string.Empty : string.Empty;
        }

        public string DataBase64 { get => _DataBase64; set { _DataBase64 = value; } }
        private string _DataBase64;
    }
}

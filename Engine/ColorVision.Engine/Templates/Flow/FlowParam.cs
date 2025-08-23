#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Dao;
using CVCommCore;
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

        public FlowParam(ModMasterModel dbModel, List<ModDetailModel> flowDetail) : base()
        {
            ModMaster = dbModel;
            Id = dbModel.Id;
            Name = dbModel.Name ?? string.Empty;
            List<Templates.ModDetailModel> modDetailModels = new();
            foreach (var model in flowDetail)
            {
                Templates.ModDetailModel mod = new() { Id = model.Id, Pid = model.Pid, IsDelete = model.IsDelete, IsEnable = model.IsEnable, SysPid = model.SysPid, ValueA = model.ValueA, ValueB = model.ValueB };
                modDetailModels.Add(mod);
                _DataBase64 = model.Value ?? string.Empty;
            }
            AddDetail(modDetailModels);
        }

        private string _DataBase64;
        public string DataBase64 { get => _DataBase64; set { _DataBase64 = value; } }

        private const string propertyName = "filename";

        public string? ResId
        {
            set { SetProperty(ref _ResId, value?.ToString(), propertyName); }
            get => GetValue(_ResId, propertyName);
        }
        private string? _ResId;
    }
}

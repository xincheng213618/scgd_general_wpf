#pragma warning disable CA1822
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Dao;
using CVCommCore;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Flow
{

    public static class FlowParamExtension
    {
        public static void Save(this FlowParam flowParam)
        {
            FlowParam.Save2DB(flowParam);
        }
    }

    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamModBase
    {
        private static ModMasterDao masterFlowDao = new("flow");
        public static void Save2DB(FlowParam flowParam)
        {
            if (ModMasterDao.Instance.GetById(flowParam.Id) is ModMasterModel modMasterModel && modMasterModel.Pcode != null)
            {
                modMasterModel.Name = flowParam.Name;
                ModMasterDao modMasterDao = new(modMasterModel.Pcode);
                modMasterDao.Save(modMasterModel);
            }

            List<ModDetailModel> list = new();
            flowParam.GetDetail(list);
            if (list.Count > 0 && list[0] is ModDetailModel model)
            {
                if (int.TryParse(model.ValueA, out int id))
                {
                    SysResourceModel res = VSysResourceDao.Instance.GetById(id);
                    if (res != null)
                    {
                        res.Code = Cryptography.GetMd5Hash(flowParam.DataBase64);
                        res.Name = flowParam.Name;
                        res.Value = flowParam.DataBase64;
                        VSysResourceDao.Instance.Save(res);
                    }
                    else
                    {
                        res = new SysResourceModel();
                        res.Name = flowParam.Name;
                        res.Type = (int)PhysicalResourceType.FlowFile;
                        if (!string.IsNullOrEmpty(flowParam.DataBase64))
                        {
                            res.Code = flowParam.Id + Cryptography.GetMd5Hash(flowParam.DataBase64);
                            res.Value = flowParam.DataBase64;
                        }
                        VSysResourceDao.Instance.Save(res);
                        model.ValueA = res.Id.ToString();
                    }
                }
                else
                {
                    SysResourceModel res = new();
                    res.Name = flowParam.Name;
                    res.Type = (int)PhysicalResourceType.FlowFile;
                    if (!string.IsNullOrEmpty(flowParam.DataBase64))
                    {
                        res.Code = Cryptography.GetMd5Hash(flowParam.DataBase64);
                        res.Value = flowParam.DataBase64;
                    }
                    VSysResourceDao.Instance.Save(res);
                    model.ValueA = res.Id.ToString();
                }
                ModDetailDao.Instance.UpdateByPid(flowParam.Id, list);
            }
        }

        public FlowParam()
        {

        }

        public FlowParam(ModMasterModel dbModel, List<ModFlowDetailModel> flowDetail) : base()
        {
            Id = dbModel.Id;
            Name = dbModel.Name ?? string.Empty;
            List<ModDetailModel> modDetailModels = new();
            foreach (var model in flowDetail)
            {
                ModDetailModel mod = new() { Id = model.Id, Pid = model.Pid, IsDelete = model.IsDelete, IsEnable = model.IsEnable, Symbol = model.Symbol, SysPid = model.SysPid, ValueA = model.ValueA, ValueB = model.ValueB };
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

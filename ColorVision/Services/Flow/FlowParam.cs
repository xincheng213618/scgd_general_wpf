using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Flow.Dao;
using ColorVision.Services.Templates;
using ColorVision.Services.Templates.POI;
using ColorVision.Settings;
using ColorVision.UI;
using ColorVision.UserSpace;
using CVCommCore;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Flow
{
    public class ExportFlow : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "FlowParam";
        public int Order => 0;
        public string? Header => Properties.Resource.MenuFlow;

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
        public static ObservableCollection<TemplateModel<FlowParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();

        private static ModMasterDao masterFlowDao = new ModMasterDao(ModMasterType.Flow);

        public static ObservableCollection<TemplateModel<FlowParam>> LoadFlowParam()
        {
            Params.Clear();
            if (ConfigHandler.GetInstance().SoftwareConfig.IsUseMySql)
            {
                List<ModMasterModel> flows = masterFlowDao.GetAll(UserCenter.GetInstance().TenantId);
                foreach (var dbModel in flows)
                {
                    List<ModFlowDetailModel> flowDetails = ModFlowDetailDao.Instance.GetAllByPid(dbModel.Id);
                    var item = new TemplateModel<FlowParam>(dbModel.Name ?? "default", new FlowParam(dbModel, flowDetails));
                    Params.Add(item);
                }
            }
            return Params;
        }

        public static FlowParam? AddFlowParam(string text)
        {
            ModMasterModel flowMaster = new ModMasterModel(ModMasterType.Flow, text, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
            Save(flowMaster);

            int pkId = flowMaster.Id;
            if (pkId > 0)
            {
                List<ModFlowDetailModel> flowDetail = ModFlowDetailDao.Instance.GetAllByPid(pkId);
                if (int.TryParse(flowDetail[0].ValueA, out int id))
                {
                    SysResourceModel sysResourceModeldefault = VSysResourceDao.Instance.GetById(id);
                    if (sysResourceModeldefault != null)
                    {
                        SysResourceModel sysResourceModel = new SysResourceModel();
                        sysResourceModel.Name = flowMaster.Name;
                        sysResourceModel.Code = sysResourceModeldefault.Code;
                        sysResourceModel.Type = sysResourceModeldefault.Type;
                        sysResourceModel.Value = sysResourceModeldefault.Value;
                        VSysResourceDao.Instance.Save(sysResourceModel);
                        flowDetail[0].ValueA = sysResourceModel.Id.ToString();
                        ModFlowDetailDao.Instance.Save(flowDetail[0]);
                    }
                }
                if (flowMaster != null) return new FlowParam(flowMaster, flowDetail);
                else return null;
            }
            return null;
        }

        public static int Save(ModMasterModel modMaster)
        {
            int ret = -1;
            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(modMaster.Pcode, modMaster.TenantId);
            if (mod != null)
            {
                modMaster.Pid = mod.Id;
                ret = ModMasterDao.Instance.Save(modMaster);
                List<ModDetailModel> list = new List<ModDetailModel>();
                List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModDetailDao.Instance.GetAllByPid(modMaster.Pid);
                foreach (var item in sysDic)
                {
                    list.Add(new ModDetailModel(item.Id, modMaster.Id, item.DefaultValue));
                }
                ModDetailDao.Instance.SaveByPid(modMaster.Id, list);
            }
            return ret;
        }

        public static void Save2DB<T>(ObservableCollection<TemplateModel<T>> keyValuePairs) where T : FlowParam
        {
            foreach (var item in keyValuePairs)
            {
                Save2DB(item.Value);
            }
        }

        public static void Save2DB(FlowParam flowParam)
        {
            List<ModDetailModel> list = new List<ModDetailModel>();
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
                    SysResourceModel res = new SysResourceModel();
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

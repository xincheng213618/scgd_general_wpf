using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Flow.Dao;
using ColorVision.Services.Templates;
using ColorVision.UI.Menus;
using ColorVision.UserSpace;
using CVCommCore;
using NPOI.SS.Formula.Functions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace ColorVision.Services.Flow
{
    public class ExportFlow : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "FlowParam";
        public int Order => 0;
        public string? Header => Properties.Resource.MenuFlow;

        public string? InputGestureText { get; }
        public Visibility Visibility => Visibility.Visible;
        public object? Icon { get; }

        public RelayCommand Command => new(a => {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class TemplateFlow : ITemplate<FlowParam>, IITemplateLoad
    {
        public TemplateFlow()
        {
            IsSideHide = true;
            Title = "流程引擎";
            Code = ModMasterType.Flow;
            TemplateParams = FlowParam.Params;
        }

        public override void PreviewMouseDoubleClick(int index)
        {
            new WindowFlowEngine(TemplateParams[index].Value) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
        }

        public override void Load() => FlowParam.LoadFlowParam();

        public override void Save() => FlowParam.Save2DB(TemplateParams);

        public override void Create(string templateName)
        {
            FlowParam? param = FlowParam.AddFlowParam(templateName);
            if (param != null)
            {
                var a = new TemplateModel<FlowParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(T)}模板失败", "ColorVision");
            }
        }
    }


    /// <summary>
    /// 流程引擎模板
    /// </summary>
    public class FlowParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<FlowParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();

        private static ModMasterDao masterFlowDao = new(ModMasterType.Flow);

        public static void LoadFlowParam()
        {
            var backup = Params.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                List<ModMasterModel> flows = masterFlowDao.GetAll(UserConfig.Instance.TenantId);
                foreach (var dbModel in flows)
                {
                    List<ModFlowDetailModel> flowDetails = ModFlowDetailDao.Instance.GetAllByPid(dbModel.Id);
                    var param = new FlowParam(dbModel, flowDetails);

                    if (backup.TryGetValue(param.Id,out var model))
                    {
                        model.Value = param;
                        model.Key = param.Name;

                    }
                    else
                    {
                        var item = new TemplateModel<FlowParam>(dbModel.Name ?? "default", param);
                        Params.Add(item);
                    }


                }
            }
        }

        public static FlowParam? AddFlowParam(string text)
        {
            ModMasterModel flowMaster = new(ModMasterType.Flow, text, UserConfig.Instance.TenantId);
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
                        SysResourceModel sysResourceModel = new();
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
                List<ModDetailModel> list = new();
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

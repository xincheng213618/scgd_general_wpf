using ColorVision.Database;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.Engine.Templates.Validate.Dic;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Validate
{

    public class TemplateComplyParam : ITemplate<ValidateParam>
    {
        public static Dictionary<string, ObservableCollection<TemplateModel<ValidateParam>>> CIEParams { get; set; } = new Dictionary<string, ObservableCollection<TemplateModel<ValidateParam>>>();
        public static Dictionary<string, ObservableCollection<TemplateModel<ValidateParam>>> JNDParams { get; set; } = new Dictionary<string, ObservableCollection<TemplateModel<ValidateParam>>>();

        public TemplateComplyParam()
        {

        }


        public TemplateComplyParam(string code, int type = 0)
        {
            Code = code;
            if (CIEParams.TryGetValue(Code, out var templatesParams))
            {
                TemplateParams = templatesParams;
            }
            else if (JNDParams.TryGetValue(Code, out var templateModels))
            {
                TemplateParams = templateModels;
            }
            else
            {
                templatesParams = new ObservableCollection<TemplateModel<ValidateParam>>();
                TemplateParams = templatesParams;

                if (type == 1)
                {
                    JNDParams.Add(Code, templatesParams);
                }
                {
                    CIEParams.Add(Code, templatesParams);
                }

            }
            IsUserControl = true;
            ValidateControl = new ValidateControl();
        }



        public override string Title { get => Code + Properties.Resources.Edit; set { } }

        public ValidateControl ValidateControl { get; set; }

        public override UserControl GetUserControl() => ValidateControl;

        public override void SetUserControlDataContext(int index)
        {
            ValidateControl.SetParam(TemplateParams[index].Value);
        }

        public override void Load()
        {
            if (!(MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect))
                return;

            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            SysDictionaryModModel mod = SysDictionaryModMasterDao.Instance.GetByCode(Code, UserConfig.Instance.TenantId);
            if (mod == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"找不到字典{Code}", "Template");
                return;
            }
            var models = ValidateTemplateMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "dic_pid", mod.Id }, { "is_delete", 0 }, { "tenant_id", UserConfig.Instance.TenantId } });
            foreach (var dbModel in models)
            {
                var details = ValidateTemplateDetailDao.Instance.GetAllByPid(dbModel.Id);
                var validateParam = new ValidateParam(dbModel, details);

                if (backup.TryGetValue(validateParam.Id, out var model))
                {
                    model.Value = validateParam;
                    model.Key = validateParam.Name;
                }
                else
                {
                    TemplateParams.Add(new TemplateModel<ValidateParam>(dbModel.Name ?? "default", validateParam));
                }
            }
        }

        public override void Delete(int index)
        {
            if (index >= 0 && index < TemplateParams.Count)
            {
                int id = TemplateParams[index].Value.Id;

                Db.Deleteable<ValidateTemplateDetailModel>().Where(x => x.Pid == id).ExecuteCommand();
                Db.Deleteable<ValidateTemplateMasterModel>().Where(x => x.Id == id).ExecuteCommand();
            }
        }


        public override void Save()
        {
            foreach (var item in TemplateParams)
            {
                if (ValidateTemplateMasterDao.Instance.GetById(item.Value.Id) is ValidateTemplateMasterModel modMasterModel)
                {
                    modMasterModel.Name = item.Value.Name;
                    ValidateTemplateMasterDao.Instance.Save(modMasterModel);
                }
                foreach (var detailmodel in item.Value.DetailModels)
                {
                    ValidateTemplateDetailDao.Instance.Save(detailmodel);
                }
            }

        }

        public override bool Import()
        {
            MessageBox.Show(Application.Current.GetActiveWindow(), $"暂不支持模板{Code}的导入", "ColorVision");
            return false;
        }

        public override void Create(string templateName)
        {
            ValidateTemplateMasterModel modMaster = new ValidateTemplateMasterModel() { Code = Code, Name = templateName, TenantId = UserConfig.Instance.TenantId };

            SysDictionaryModModel mod = SysDictionaryModMasterDao.Instance.GetByCode(Code, UserConfig.Instance.TenantId);
            if (mod != null)
            {
                modMaster.DId = mod.Id;
                int ret = ValidateTemplateMasterDao.Instance.Save(modMaster);

                var sysDic = SysDictionaryModItemValidateDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "pid", mod.Id }, { "is_enable", true } });
                foreach (var item in sysDic)
                {
                    var ss = new ValidateTemplateDetailModel() { Code = item.Code, DicPid = mod.Id, Pid = modMaster.Id, ValMax = item.ValMax, ValEqual = item.ValEqual, ValMin = item.ValMin, ValRadix = item.ValRadix, ValType = item.ValType };
                    ValidateTemplateDetailDao.Instance.Save(ss);
                }

                var Details = ValidateTemplateDetailDao.Instance.GetAllByPid(modMaster.Id);
                modMaster = ValidateTemplateMasterDao.Instance.GetById(modMaster.Id);
                if (modMaster != null)
                {
                    TemplateParams.Add(new TemplateModel<ValidateParam>(modMaster.Name ?? "default", new ValidateParam(modMaster, Details)));
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(ValidateParam)}模板失败", "ColorVision");
                }



            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(ValidateParam)}模板失败", "ColorVision");
            }

        }


    }
}

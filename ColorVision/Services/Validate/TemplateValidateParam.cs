using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.Services.Validate.Dao;
using ColorVision.UserSpace;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Validate
{
    public class TemplateValidateParam : ITemplate<ValidateParam>, IITemplateLoad
    {
        public TemplateValidateParam()
        {
            Title = "ValidateParam";
            TemplateParams = ValidateParam.CIEParams;
            Code = "Validate.CIE";
            IsUserControl = true;
            ValidateControl = new ValidateControl();
        }


        public ValidateControl ValidateControl { get; set; }

        public override UserControl GetUserControl() => ValidateControl;

        public override void SetUserControlDataContext(int index)
        {
            ValidateControl.SetParam(TemplateParams[index].Value);
        }

        public override void Load()
        {
            TemplateParams.Clear();
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(Code, UserConfig.Instance.TenantId);
                if (mod == null)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"找不到字典{Code}", "Template");
                    return;
                }
                var models = ValidateTemplateMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { {"dic_pid",mod.Id },{ "is_delete", 0 }, { "tenant_id", UserConfig.Instance.TenantId } });
                foreach (var dbModel in models)
                {
                    var Details = ValidateTemplateDetailDao.Instance.GetAllByPid(dbModel.Id);
                    TemplateParams.Add(new TemplateModel<ValidateParam>(dbModel.Name ?? "default", new ValidateParam(dbModel, Details)));
                }
            }
        }

        public override void Delete(int index)
        {
            if (index >= 0 && index < TemplateParams.Count)
            {
                int id = TemplateParams[index].Value.Id;
                int ret = ValidateTemplateMasterDao.Instance.DeleteById(id);
                ValidateTemplateDetailDao.Instance.DeleteAllByPid(id);
                TemplateParams.RemoveAt(index);
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

        public override void Create(string templateName)
        {
            ValidateTemplateMasterModel modMaster = new ValidateTemplateMasterModel() { Code = Code, Name = templateName, TenantId = UserConfig.Instance.TenantId };

            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(Code, UserConfig.Instance.TenantId);
            if (mod != null)
            {
                modMaster.DId = mod.Id;
                int ret = ValidateTemplateMasterDao.Instance.Save(modMaster);

                var sysDic = SysDictionaryModItemValidateDao.Instance.GetAllByPid(mod.Id);
                foreach (var item in sysDic)
                {
                    var ss = new ValidateTemplateDetailModel() { Code = item.Code ,DicPid = mod.Id,Pid = modMaster .Id,ValMax = item.ValMax,ValEqual =item.ValEqual , ValMin =item.ValMin, ValRadix =item.ValRadix ,ValType =item.ValType};
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

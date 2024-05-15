using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.DAO.Validate;
using ColorVision.Services.Templates;
using ColorVision.UI;
using ColorVision.UserSpace;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Dao.Validate
{

    public class ExportValidue : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "Validue";
        public int Order => 2;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => ColorVision.Properties.Resource.MenuValidue;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a => {
            new WindowTemplate(new TemplateValidateParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class TemplateValidateParam : ITemplate<ValidateParam>, IITemplateLoad
    {
        public TemplateValidateParam()
        {
            Title = "ValidateParam";
            TemplateParams = ValidateParam.Params;
            Code = "Validate.CIE";
        }

        public override void Load()
        {
            TemplateParams.Clear();

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var models = ValidateTemplateMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "is_delete", 0 }, { "tenant_id", UserConfig.Instance.TenantId } });
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

        public override void Create(string templateName)
        {
            ValidateTemplateMasterModel modMaster = new ValidateTemplateMasterModel() {  Code =Code,Name = templateName  ,TenantId =UserConfig.Instance.TenantId} ;

            SysDictionaryModModel mod = SysDictionaryModDao.Instance.GetByCode(Code, UserConfig.Instance.TenantId);
            if (mod != null)
            {
                modMaster.DId = mod.Id;
                int ret = ValidateTemplateMasterDao.Instance.Save(modMaster);
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


                //List<ValidateTemplateDetailModel> list = new();
                //List<SysDictionaryModDetaiModel> sysDic = SysDictionaryModItemValidateDao.Instance.GetAllByPid(mod.Id);
                //foreach (var item in sysDic)
                //{
                //    list.Add(new ValidateTemplateDetailModel(item.Id, modMaster.Id, item.DefaultValue));
                //}
                //ValidateTemplateDetailDao.Instance.SaveByPid(modMaster.Id, list);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"数据库创建{typeof(ValidateParam)}模板失败", "ColorVision");
            }

        }


    }

    public class ValidateParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();

        public ValidateParam()
        {

        }

        private List<ValidateTemplateDetailModel> validateTemplateDetailModels = new List<ValidateTemplateDetailModel>();

        public ValidateParam(ValidateTemplateMasterModel modMaster, List<ValidateTemplateDetailModel> modDetails) 
        {
            Id = modMaster.Id;
            validateTemplateDetailModels = modDetails;
        }
    }
}

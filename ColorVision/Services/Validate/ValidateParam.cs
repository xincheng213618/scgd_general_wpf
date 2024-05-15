using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.UI;
using ColorVision.UserSpace;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace ColorVision.Services.Validate
{

    public class ExportValidue : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "Validue";
        public int Order => 4;
        public Visibility Visibility => Visibility.Visible;
        public string? Header => Properties.Resource.MenuValidue;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a =>
        {
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
                var Details = ValidateTemplateDetailDao.Instance.GetAllByPid(modMaster.Id);

                List<ValidateTemplateDetailModel> list = new();
                var sysDic = SysDictionaryModItemValidateDao.Instance.GetAllByPid(mod.Id);
                foreach (var item in sysDic)
                {
                    var ss = new ValidateTemplateDetailModel() { Code = item.Code ,DicPid = mod.Id,Pid = modMaster .Id,ValMax = item.ValMax,ValEqual =item.ValEqual , ValMin =item.ValMin, ValRadix =item.ValRadix ,ValType =item.ValType};
                    list.Add(ss);
                    ValidateTemplateDetailDao.Instance.Save(ss);
                }


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

    public class ValidateParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();

        public ValidateParam()
        {

        }

        public List<ValidateTemplateDetailModel> DetailModels { get; set; } = new List<ValidateTemplateDetailModel>();
        private Dictionary<string, ValidateTemplateDetailModel> parameters = new Dictionary<string, ValidateTemplateDetailModel>();

        public ValidateParam(ValidateTemplateMasterModel modMaster, List<ValidateTemplateDetailModel> modDetails)
        {
            Id = modMaster.Id;
            DetailModels = modDetails;
            foreach (var DetailModel in modDetails)
            {
                if (DetailModel.Code != null)
                {
                    if (!parameters.ContainsKey(DetailModel.Code))
                        parameters.Add(DetailModel.Code, DetailModel);
                }
            }
            if (parameters.TryGetValue("x",out var x))
                X = new ValidateSingle(x);
            if (parameters.TryGetValue("y", out var y))
                Y = new ValidateSingle(y);
            if (parameters.TryGetValue("u", out var u))
                U = new ValidateSingle(u);
            if (parameters.TryGetValue("v", out var v))
                V = new ValidateSingle(v);
            if (parameters.TryGetValue("lv", out var lv))
                Lv = new ValidateSingle(lv);
        }


        public ValidateSingle X { get; set; }
        public ValidateSingle Y { get; set; }
        public ValidateSingle U { get; set; }
        public ValidateSingle V { get; set; }
        public ValidateSingle Lv { get; set; }

    }


    public class ValidateSingle : ViewModelBase
    {
        private ValidateTemplateDetailModel model;
        public ValidateSingle(ValidateTemplateDetailModel modDetails)
        {
            model = modDetails;
        }
        public float ValMax { get => model.ValMax; set { model.ValMax = value; NotifyPropertyChanged(); } }
        public float ValMin { get => model.ValMin; set { model.ValMin = value; NotifyPropertyChanged(); } }
        public string? ValEqual { get => model.ValEqual; set { model.ValEqual = value; NotifyPropertyChanged(); } }
        public short ValRadix { get => model.ValRadix; set { model.ValRadix = value; NotifyPropertyChanged(); } }
        public short ValType { get => model.ValType; set { model.ValType = value; NotifyPropertyChanged(); } }
    }
}

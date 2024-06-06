using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.Engine.Templates.POI.Validate;
using ColorVision.UserSpace;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.SysDictionary
{

    public class TemplateDicModParam : ITemplate<DicModParam>, IITemplateLoad
    {
        public TemplateDicModParam()
        {
            Title = "DicModParam";
            TemplateParams = DicModParam.Params;
            IsUserControl = true;  
        }

        public EditDictionaryMode EditDictionaryMode { get; set; } = new EditDictionaryMode();

        public override UserControl GetUserControl()
        {
            return EditDictionaryMode;
        }
        public override void SetUserControlDataContext(int index)
        {
            EditDictionaryMode.SetParam(TemplateParams[index].Value);
        }

        public override void Load()
        {
            TemplateParams.Clear();
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var models = SysDictionaryModDao.Instance.GetAllByTenantId(UserConfig.Instance.TenantId);
                foreach (var model in models)
                {
                   var list =  SysDictionaryModDetailDao.Instance.GetAllByPid(model.Id);
                    TemplateParams.Add(new TemplateModel<DicModParam>(model.Name ?? "default", new DicModParam(model, list)));
                }
            }
        }

        public override void Save()
        {
            if (SaveIndex.Count == 0) return;

            foreach (var index in SaveIndex)
            {
                if (index > -1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];
                    var modMasterModel = SysDictionaryModDao.Instance.GetById(item.Value.Id);

                    foreach (var modDetaiModel in TemplateParams[index].Value.ModDetaiModels)
                    {
                        SysDictionaryModDetailDao.Instance.Save(modDetaiModel);
                    }
                }
            }
        }

        public override void Create(string templateName)
        {
            MessageBox.Show("不允许创建");
        }
    }


    public class DicModParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<DicModParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DicModParam>>();

        public SysDictionaryModModel modMasterModel { get; set; }
        public RelayCommand CreateCommand { get; set; }

        public DicModParam()
        {
            CreateCommand = new RelayCommand(a => new CreateModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }

        public DicModParam(SysDictionaryModModel modMasterModel,List<SysDictionaryModDetaiModel> dicModParams) 
        {
            Id = modMasterModel.Id;
            Name = modMasterModel.Name ??"default";
            ModDetaiModels = new ObservableCollection<SysDictionaryModDetaiModel>(dicModParams);
            CreateCommand = new RelayCommand(a => new CreateModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }

        public ObservableCollection<SysDictionaryModDetaiModel> ModDetaiModels { get; set; }


    };
}

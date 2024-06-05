using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.Engine.Templates.POI.Validate;
using ColorVision.UserSpace;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

        public override void Create(string templateName)
        {

        }
    }


    public class DicModParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<DicModParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DicModParam>>();

        public SysDictionaryModModel modMasterModel { get; set; }

        public DicModParam()
        {

        }

        public DicModParam(SysDictionaryModModel modMasterModel,List<SysDictionaryModDetaiModel> dicModParams) 
        {
            Id = modMasterModel.Id;
            Name = modMasterModel.Name;
            ModDetaiModels = new ObservableCollection<SysDictionaryModDetaiModel>(dicModParams);
            foreach (var item in dicModParams)
            {
                ModDetaiModels.Add(item);
            }
        }

        public ObservableCollection<SysDictionaryModDetaiModel> ModDetaiModels { get; set; }


    };
}

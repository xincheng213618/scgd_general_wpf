using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.Dao;
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
            
        }

        public override void Load()
        {
            TemplateParams.Clear();
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var models = SysDictionaryModDao.Instance.GetAllByTenantId(UserConfig.Instance.TenantId);
                foreach (var model in models)
                {
                    //TemplateParams.Add(new TemplateModel<DicModParam>(model.Name ?? "default", new DicModParam(model)));
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

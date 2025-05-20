
using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Rbac;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.SysDictionary
{

    public class TemplateModParam : ITemplate<DicModParam>
    {
        public static ObservableCollection<TemplateModel<DicModParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DicModParam>>();

        public TemplateModParam()
        {
            Name= "字典模板管理";
            Title = "DicModParam";
            TemplateParams = Params;
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

        public override void Delete(int index)
        {
            if (index > -1 && index < TemplateParams.Count)
            {
                var item = TemplateParams[index];
                SysDictionaryModMasterDao.Instance.DeleteById(item.Value.Id,false);
                TemplateParams.RemoveAt(index);
            }
        }

        public override void Save(TemplateModel<DicModParam> item)
        {
            base.Save(item);
            MenuManager.GetInstance().LoadMenuItemFromAssembly();
        }

        public override void Load()
        {
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var models = SysDictionaryModMasterDao.Instance.GetAllByParam( new Dictionary<string, object>() { {"tenant_id", UserConfig.Instance.TenantId },{"mod_type",7 } });
                foreach (var model in models)
                {
                    var list = SysDictionaryModDetailDao.Instance.GetAllByPid(model.Id);
                    var t = new DicModParam(model, list);

                    if (backup.TryGetValue(t.Id, out var template))
                    {
                        template.Value = t;
                        template.Key = t.Name;
                    }
                    else
                    {
                        var templateModel = new TemplateModel<DicModParam>(t.Name ?? "default", t);
                        TemplateParams.Add(templateModel);
                    }
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
                    var modMasterModel = SysDictionaryModMasterDao.Instance.GetById(item.Value.Id);

                    foreach (var modDetaiModel in TemplateParams[index].Value.ModDetaiModels)
                    {
                        SysDictionaryModDetailDao.Instance.Save(modDetaiModel);
                    }
                }
            }
        }

        public override void OpenCreate()
        {
            CreateDicTemplate createDicTemplate = new CreateDicTemplate(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            createDicTemplate.ShowDialog();
        }

        public override void Create(string templateCode, string templateName)
        {
            SysDictionaryModModel sysDictionaryModModel = new SysDictionaryModModel() { Name = templateName, Code = templateCode, ModType = 7 };
            SysDictionaryModMasterDao.Instance.Save(sysDictionaryModModel);
            var list = SysDictionaryModDetailDao.Instance.GetAllByPid(sysDictionaryModModel.Id);
            var t = new DicModParam(sysDictionaryModModel, list);
            var templateModel = new TemplateModel<DicModParam>(t.Name ?? "default", t);
            TemplateParams.Add(templateModel);
        }
    }



    public class DicModParam : ParamModBase
    {

        public SysDictionaryModModel modMasterModel { get; set; }

        public DicModParam()
        {
            CreateCommand = new RelayCommand(a => new CreateDicModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }

        public DicModParam(SysDictionaryModModel modMasterModel,List<SysDictionaryModDetaiModel> dicModParams) 
        {
            Id = modMasterModel.Id;
            Name = modMasterModel.Name ??"default";
            ModDetaiModels = new ObservableCollection<SysDictionaryModDetaiModel>(dicModParams);
            CreateCommand = new RelayCommand(a => new CreateDicModeDetail(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(), a => true);
        }

        public ObservableCollection<SysDictionaryModDetaiModel> ModDetaiModels { get; set; }
    };
}

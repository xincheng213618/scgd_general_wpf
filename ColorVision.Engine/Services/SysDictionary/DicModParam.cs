using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.Engine.Templates.POI.Comply;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using ColorVision.Engine.Rbac;
using NPOI.SS.Formula.Functions;
using NPOI.XWPF.UserModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.SysDictionary
{
    public class ExportDicModParam : MenuItemBase
    {
        public override string OwnerGuid => "Template";

        public override string GuidId => "DicModParam";
        public override int Order => 31;
        public override string Header => "算法模板编辑";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

    public class TemplateModParam : ITemplate<DicModParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<DicModParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DicModParam>>();

        public TemplateModParam()
        {
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

        public override void Load()
        {
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var models = SysDictionaryModDao.Instance.GetAllByParam( new Dictionary<string, object>() { {"tenant_id", UserConfig.Instance.TenantId },{"mod_type",7 } });
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
                    var modMasterModel = SysDictionaryModDao.Instance.GetById(item.Value.Id);

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
            SysDictionaryModDao.Instance.Save(sysDictionaryModModel);
            var list = SysDictionaryModDetailDao.Instance.GetAllByPid(sysDictionaryModModel.Id);
            var t = new DicModParam(sysDictionaryModModel, list);
            var templateModel = new TemplateModel<DicModParam>(t.Name ?? "default", t);
            TemplateParams.Add(templateModel);
        }
    }

    public class ExportTemplateSensor : MenuItemBase
    {
        public override string OwnerGuid => "Template";

        public override string GuidId => "TemplateSensor";
        public override int Order => 31;
        public override string Header => "传感器模板编辑";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateSensorDicModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

    public class TemplateSensorDicModParam : TemplateModParam
    {
        public new static ObservableCollection<TemplateModel<DicModParam>> Params { get; set; } = new ObservableCollection<TemplateModel<DicModParam>>();

        public TemplateSensorDicModParam()
        {
            Title = "传感器模板编辑";
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void Load()
        {
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);

            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                var models = SysDictionaryModDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", UserConfig.Instance.TenantId }, { "mod_type", 5 } });
                foreach (var model in models)
                {
                    var t = new DicModParam(model,new List<SysDictionaryModDetaiModel>());
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


    }


    public class DicModParam : ParamBase
    {

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

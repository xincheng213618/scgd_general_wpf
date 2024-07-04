using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Rbac;
using ColorVision.Engine.Services.SysDictionary;
using ColorVision.Engine.Templates.POI.Comply.Dao;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.Comply
{
    public class ExportComplyEdit : MenuItemBase
    {
        public override string OwnerGuid => "Comply";

        public override string GuidId => "ComplyEdit";
        public override int Order => 99;
        public override string Header => "合规模板编辑";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateDicComply()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }


    public class ComplyParam : ParamBase
    {

        public SysDictionaryModModel modMasterModel { get; set; }
        public RelayCommand CreateCommand { get; set; }

        public ComplyParam()
        {
        }

        public ComplyParam(SysDictionaryModModel modMasterModel, List<SysDictionaryModItemValidateModel> dicModParams)
        {
            Id = modMasterModel.Id;
            Name = modMasterModel.Name ?? "default";
            ModDetaiModels = new ObservableCollection<SysDictionaryModItemValidateModel>(dicModParams);
        }

        public ObservableCollection<SysDictionaryModItemValidateModel> ModDetaiModels { get; set; }
    };



    public class TemplateDicComply : ITemplate<ComplyParam>, IITemplateLoad
    {
        public  static ObservableCollection<TemplateModel<ComplyParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ComplyParam>>();

        public TemplateDicComply()
        {
            Title = "合规模板编辑";
            TemplateParams = Params;
            IsUserControl = true;
        }




        public override void Load()
        {
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.Instance.IsUseMySql && MySqlSetting.IsConnect)
            {
                void Add(int mod_type)
                {
                    var models = SysDictionaryModDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "tenant_id", UserConfig.Instance.TenantId }, { "mod_type", mod_type } });
                    foreach (var model in models)
                    {
                        var list = SysDictionaryModItemValidateDao.Instance.GetAllByPid(model.Id);
                        var t = new ComplyParam(model, list);
                        if (backup.TryGetValue(t.Id, out var template))
                        {
                            template.Value = t;
                            template.Key = t.Name;
                        }
                        else
                        {
                            var templateModel = new TemplateModel<ComplyParam>(t.Name ?? "default", t);
                            TemplateParams.Add(templateModel);
                        }
                    }
                }
                Add(110);
                Add(111);
            }
        }
    }


}

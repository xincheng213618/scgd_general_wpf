using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Templates.POI.Dao;
using ColorVision.Services.Dao;
using ColorVision.UI.Menus;
using ColorVision.UserSpace;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.DicMod
{

    public class ExportDicModParam : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string? GuidId => "DicModParam";
        public int Order => 31;
        public string? Header => "DicMod";
        public Visibility Visibility => Visibility.Visible;
        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(a =>
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateDicModParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }


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
                    TemplateParams.Add(new TemplateModel<DicModParam>(model.Name ?? "default", new DicModParam(model)));
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

        public DicModParam(SysDictionaryModModel modMasterModel):base()
        {
            Id = modMasterModel.Id;
            Name = modMasterModel.Name;
        }


    };
}

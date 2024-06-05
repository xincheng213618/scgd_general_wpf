#pragma warning disable CA1707,IDE1006

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates
{

    public class ExportFocusPoints : IMenuItem
    {
        public string? OwnerGuid => "TemplateAlgorithm";

        public string? GuidId => "FocusPoints";
        public int Order => 2;
        public string? Header => ColorVision.Engine.Properties.Resources.MenuFocusPoints;
        public Visibility Visibility => Visibility.Visible;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a => {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateFocusPointsParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class TemplateFocusPointsParam : ITemplate<FocusPointsParam>, IITemplateLoad
    {
        public TemplateFocusPointsParam()
        {
            Title = "FocusPoints算法设置";
            Code = ModMasterType.FocusPoints;
            TemplateParams = FocusPointsParam.FocusPointsParams;
        }
    }

    public class FocusPointsParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<FocusPointsParam>> FocusPointsParams { get; set; } = new ObservableCollection<TemplateModel<FocusPointsParam>>();

        public FocusPointsParam() { }
        public FocusPointsParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        public bool value1 { get; set; }


    }
}

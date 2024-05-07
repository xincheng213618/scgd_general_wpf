#pragma warning disable CA1707,IDE1006

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Devices.Algorithm.Templates
{

    public class MenuItemFocusPoints : IMenuItem
    {
        public string? OwnerGuid => "TemplateAlgorithm";

        public string? GuidId => "FocusPoints";
        public int Order => 2;
        public string? Header => "发光区检测模板设置(_F)";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new RelayCommand(a => {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(TemplateType.FocusPointsParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
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

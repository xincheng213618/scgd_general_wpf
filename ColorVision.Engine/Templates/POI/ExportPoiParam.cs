using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.POI
{
    public class ExportPoiParam : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "PoiParam";
        public int Order => 1;
        public string? Header => Properties.Resources.MenuPoi;

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new(a =>
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplatePOI()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
        public Visibility Visibility => Visibility.Visible;

    }

}

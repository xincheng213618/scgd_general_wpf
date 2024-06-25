using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.POI.Validate.Dao;
using ColorVision.Engine.Templates.POI.Validate;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using ColorVision.Themes;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class ExportSensor : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string? GuidId => "Sensor";
        public int Order => 21;
        public string? Header => Properties.Resources.MenuSensor;

        public string? InputGestureText { get; }

        public object? Icon { get; }
        public Visibility Visibility => Visibility.Visible;

        public RelayCommand Command => new(a => {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
        });
    }

}

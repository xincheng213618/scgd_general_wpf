using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Services.Dao;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Services.Devices.Sensor.Templates
{
    public class ExportSensorHeYuan : IMenuItem
    {
        public string OwnerGuid => "Sensor";

        public string? GuidId => "SensorHeYuan";
        public int Order => 21;
        public string? Header => ColorVision.Engine.Properties.Resources.MenuSensorHeYuan;

        public string? InputGestureText { get; }

        public object? Icon { get; }
        public Visibility Visibility => Visibility.Visible;

        public RelayCommand Command => new(a => {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateSensorHeYuan()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }


    public class TemplateSensorHeYuan : ITemplate<SensorHeYuan>, IITemplateLoad
    {
        public TemplateSensorHeYuan()
        {
            Title = "SensorHeYuan设置";
            Code = "Sensor.HeYuan";
            TemplateParams = SensorHeYuan.SensorHeYuans;
        }
    }

    public class SensorHeYuan:ParamBase
    {
        public static ObservableCollection<TemplateModel<SensorHeYuan>> SensorHeYuans { get; set; } = new ObservableCollection<TemplateModel<SensorHeYuan>>();

        public SensorHeYuan() : base()
        {

        }

        public SensorHeYuan(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {

        }

    }
}

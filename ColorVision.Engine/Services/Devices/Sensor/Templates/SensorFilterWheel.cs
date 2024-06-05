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
    public class ExportFilterWheel : IMenuItem
    {
        public string OwnerGuid => "Sensor";

        public string? GuidId => "SensorFilterWheel";
        public int Order => 21;
        public string? Header => "FilterWheel";

        public string? InputGestureText { get; }

        public object? Icon { get; }
        public Visibility Visibility => Visibility.Visible;

        public RelayCommand Command => new(a => {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateFilterWheel()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }


    public class TemplateFilterWheel : ITemplate<SensorFilterWheel>, IITemplateLoad
    {
        public TemplateFilterWheel()
        {
            Title = "SensorHeYuan设置";
            Code = "Sensor.FW";
            TemplateParams = SensorFilterWheel.Params;
        }
    }

    public class SensorFilterWheel : ParamBase
    {
        public static ObservableCollection<TemplateModel<SensorFilterWheel>> Params { get; set; } = new ObservableCollection<TemplateModel<SensorFilterWheel>>();

        public SensorFilterWheel() : base()
        {

        }

        public SensorFilterWheel(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {

        }

        public string? Move { get => GetValue(_Move); set { SetProperty(ref _Move, value); } }
        private string? _Move = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";




    }
}

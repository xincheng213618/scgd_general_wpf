using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
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

        public string? Move0 { get => GetValue(_Move0); set { SetProperty(ref _Move0, value); } }
        private string? _Move0 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move1 { get => GetValue(_Move1); set { SetProperty(ref _Move1, value); } }
        private string? _Move1 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move2 { get => GetValue(_Move2); set { SetProperty(ref _Move2, value); } }
        private string? _Move2 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move3 { get => GetValue(_Move3); set { SetProperty(ref _Move3, value); } }
        private string? _Move3 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move4 { get => GetValue(_Move4); set { SetProperty(ref _Move4, value); } }
        private string? _Move4 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move5 { get => GetValue(_Move5); set { SetProperty(ref _Move5, value); } }
        private string? _Move5 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move6 { get => GetValue(_Move6); set { SetProperty(ref _Move6, value); } }
        private string? _Move6 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move7 { get => GetValue(_Move7); set { SetProperty(ref _Move7, value); } }
        private string? _Move7 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move8 { get => GetValue(_Move8); set { SetProperty(ref _Move8, value); } }
        private string? _Move8 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        public string? Move9 { get => GetValue(_Move9); set { SetProperty(ref _Move9, value); } }
        private string? _Move9 = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

    }
}

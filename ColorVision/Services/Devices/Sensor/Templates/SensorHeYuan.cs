using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Devices.Sensor.Templates
{

    public class SensorHeYuanMenuItem : IPlugin
    {
        public string Name => "SensorHeYuan";
        public string Description => "SensorHeYuan";

        public void Execute()
        {
            MenuItem menuItem = new MenuItem() { Header = "河源PG模板设置(_B)" };
            menuItem.Click += (s, e) =>
            {
                new WindowTemplate(TemplateType.SensorHeYuan) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            };
            MenuManager.GetInstance().GetTemplateMenuItem()?.Items.Add(menuItem);
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

        [Category("SensorHeYuan"), Description("上电")]
        public string? PowerOn { get => GetValue(_PowerOn); set { SetProperty(ref _PowerOn, value); } }
        private string? _PowerOn = "5A 05 07 02 31 02 9B,5A 06 08 03 31 02 00 9E";

        [Category("SensorHeYuan"), Description("红亮")]
        public string? RedOn { get => GetValue(_RedOn); set { SetProperty(ref _RedOn, value); } }
        private string? _RedOn = "5A 05 09 04 01 FE 00 01 6C,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("红灭")]
        public string? RedOff { get => GetValue(_RedOff); set { SetProperty(ref _RedOff, value); } }
        private string? _RedOff = "5A 05 09 04 01 FE 00 00 6B,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("AMBER亮")]
        public string? AMBEROn { get => GetValue(_AMBEROn); set { SetProperty(ref _AMBEROn, value); } }
        private string? _AMBEROn = "5A 05 09 04 01 FE 01 01 6D,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("AMBER灭")]
        public string? AMBEROff { get => GetValue(_AMBEROff); set { SetProperty(ref _AMBEROff, value); } }
        private string? _AMBEROff = "5A 05 09 04 01 FE 01 00 6C,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("白亮")]
        public string? WHITEOn { get => GetValue(_WHITEOn); set { SetProperty(ref _WHITEOn, value); } }
        private string? _WHITEOn = "5A 05 09 04 01 FE 02 01 6E,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("白灭")]
        public string? WHITEOff { get => GetValue(_WHITEOff); set { SetProperty(ref _WHITEOff, value); } }
        private string? _WHITEOff = "5A 05 09 04 01 FE 02 00 6D,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("蓝亮")]
        public string? BLUEOn { get => GetValue(_BLUEOn); set { SetProperty(ref _BLUEOn, value); } }
        private string? _BLUEOn = "5A 05 09 04 01 FE 03 01 6F,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("蓝灭")]
        public string? BLUEOff { get => GetValue(_BLUEOff); set { SetProperty(ref _BLUEOff, value); } }
        private string? _BLUEOff = "5A 05 09 04 01 FE 03 00 6E,5A 06 08 03 01 FE 00 6A";

        [Category("SensorHeYuan"), Description("下电")]
        public string? PowerOff { get => GetValue(_PowerOff); set { SetProperty(ref _PowerOff, value); } }
        private string? _PowerOff = "5A 05 07 02 31 FF 98,5A 06 08 03 31 FF 00 9B";
    }
}

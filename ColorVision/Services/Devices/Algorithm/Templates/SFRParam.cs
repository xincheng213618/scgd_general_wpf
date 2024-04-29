using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.UI;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Services.Devices.Algorithm.Templates
{

    public class MenuItemSFRParam : IMenuItem
    {
        public string? OwnerGuid => "TemplateAlgorithm";

        public string? GuidId => "SFRParam";
        public int Index => 2;
        public string? Header => "SFR模板设置(_M)";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new RelayCommand(a => {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(TemplateType.SFRParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class SFRParam : ParamBase
    {
        public SFRParam() { }
        public SFRParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        [Category("SFR"), Description("Gamma")]
        public double Gamma { get => GetValue(_Gamma); set { SetProperty(ref _Gamma, value); } }
        private double _Gamma = 0.01;

        [Category("SFR"), Description("ROI x"), DisplayName("ROI X")]

        public int X { get => GetValue(_X); set { SetProperty(ref _X, value); } }
        private int _X;
        [Category("SFR"), Description("ROI y"), DisplayName("ROI Y")]
        public int Y { get => GetValue(_Y); set { SetProperty(ref _Y, value); } }
        private int _Y;
        [Category("SFR"), Description("ROI Width"), DisplayName("ROI Width")]
        public int Width { get => GetValue(_Width); set { SetProperty(ref _Width, value); } }
        private int _Width = 1000;
        [Category("SFR"), Description("ROI Height"), DisplayName("ROI Height")]
        public int Height { get => GetValue(_Height); set { SetProperty(ref _Height, value); } }
        private int _Height = 1000;

        [Category("SFR"), Description("ROI"), Browsable(false)]
        public CRECT ROI { get => new CRECT() { x = X, y = Y, cx = Width, cy = Height }; }
    }
}

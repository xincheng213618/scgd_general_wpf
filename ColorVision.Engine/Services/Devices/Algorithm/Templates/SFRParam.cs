using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.UI.Menus;
using cvColorVision;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Services.Devices.Algorithm.Templates
{

    public class ExportSFRParam : IMenuItem
    {
        public string? OwnerGuid => "TemplateAlgorithm";

        public string? GuidId => "SFRParam";
        public int Order => 2;
        public string? Header => ColorVision.Engine.Properties.Resources.MenuSFR;
        public Visibility Visibility => Visibility.Visible;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(a => {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DatabaseConnectionFailed, "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateSFRParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }
    public class TemplateSFRParam : ITemplate<SFRParam>,IITemplateLoad
    {
        public TemplateSFRParam()
        {
            Title = "SFRParam算法设置";
            Code = ModMasterType.SFR;
            TemplateParams = SFRParam.SFRParams;
        }
    }

    public class SFRParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<SFRParam>> SFRParams { get; set; } = new ObservableCollection<TemplateModel<SFRParam>>();

        public SFRParam() 
        {
        }
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
        public CRECT ROI { get => new() { x = X, y = Y, cx = Width, cy = Height }; }
    }
}

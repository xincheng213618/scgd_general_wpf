#pragma warning disable CA1707,IDE1006

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.UI;
using cvColorVision;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Services.Devices.Algorithm.Templates
{

    public class MenuItemMTFParam : IMenuItem
    {
        public string? OwnerGuid => "TemplateAlgorithm";

        public string? GuidId => "MTFParam";
        public int Order => 2;
        public string? Header => "MTF模板设置(_M)";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new RelayCommand(a => {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(TemplateType.MTFParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class MTFParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<MTFParam>> MTFParams { get; set; } = new ObservableCollection<TemplateModel<MTFParam>>();

        public MTFParam() { }
        public MTFParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {
        }

        [Category("MTF"), Description("MTF dRatio")]
        public double MTF_dRatio { get => GetValue(_MTF_dRatio); set { SetProperty(ref _MTF_dRatio, value); } }
        private double _MTF_dRatio = 0.01;


        [Category("MTF"), Description("EvaFunc")]
        public EvaFunc eEvaFunc { get => GetValue(_eEvaFunc); set { SetProperty(ref _eEvaFunc, value); } }
        private EvaFunc _eEvaFunc = EvaFunc.CalResol;


        [Category("MTF"), Description("dx")]
        public int dx { get => GetValue(_dx); set { SetProperty(ref _dx, value); } }
        private int _dx;

        [Category("MTF"), Description("dy")]
        public int dy { get => GetValue(_dy); set { SetProperty(ref _dy, value); } }
        private int _dy = 1;

        [Category("MTF"), Description("dx")]
        public int ksize { get => GetValue(_ksize); set { SetProperty(ref _ksize, value); } }
        private int _ksize = 5;

    }
}

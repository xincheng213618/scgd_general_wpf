using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Dao;
using ColorVision.Settings;
using ColorVision.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Services.Templates
{
    public class CamerExpMenuItem : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string? GuidId => "CameraExposureParam";
        public int Order => 22;
        public string? Header => "相机曝光模板设置(_B)";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new RelayCommand(a =>
        {
            SoftwareConfig SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (SoftwareConfig.IsUseMySql && !SoftwareConfig.MySqlControl.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(TemplateType.CameraExposureParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class CameraExposureParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<CameraExposureParam>> CameraExposureParams { get; set; } = new ObservableCollection<TemplateModel<CameraExposureParam>>();

        public CameraExposureParam() : base()
        {
        }

        public CameraExposureParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster.Id, modMaster.Name ?? string.Empty, modDetails)
        {

        }


        [Category("CamerExp"), Description("ExpTime")]
        public int ExpTime { get => GetValue(_ExpTime); set { SetProperty(ref _ExpTime, value); } }
        private int _ExpTime = 10;


        [Category("CamerExp"), Description("ExpTimeR")]
        public int ExpTimeR { get => GetValue(_ExpTimeR); set { SetProperty(ref _ExpTimeR, value); } }
        private int _ExpTimeR = 10;


        [Category("CamerExp"), Description("ExpTimeG")]
        public int ExpTimeG { get => GetValue(_ExpTimeG); set { SetProperty(ref _ExpTimeG, value); } }
        private int _ExpTimeG = 10;


        [Category("CamerExp"), Description("ExpTimeB")]
        public int ExpTimeB { get => GetValue(_ExpTimeB); set { SetProperty(ref _ExpTimeB, value); } }
        private int _ExpTimeB = 10;

    }

}

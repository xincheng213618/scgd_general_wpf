using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    public class ExportCameraExp : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string? GuidId => "CameraExposureParam";
        public int Order => 22;
        public string? Header => Properties.Resources.MenuCameraExp;

        public string? InputGestureText { get; }
        public Visibility Visibility => Visibility.Visible;
        public object? Icon { get; }

        public RelayCommand Command => new(a =>
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new WindowTemplate(new TemplateCameraExposureParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class TemplateCameraExposureParam : ITemplate<CameraExposureParam>, IITemplateLoad
    {
        public TemplateCameraExposureParam()
        {
            Title = "CameraExposureParam设置";
            Code = ModMasterType.CameraExposure;
            TemplateParams = CameraExposureParam.Params;
        }
    }

    public class CameraExposureParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<CameraExposureParam>> Params { get; set; } = new ObservableCollection<TemplateModel<CameraExposureParam>>();

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

using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Camera.Templates.CameraExposure
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

        public ICommand Command => new RelayCommand(a =>
        {
            if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "数据库连接失败，请先连接数据库在操作", "ColorVision");
                return;
            }
            new TemplateEditorWindow(new TemplateCameraExposureParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

}

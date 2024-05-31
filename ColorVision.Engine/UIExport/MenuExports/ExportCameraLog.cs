#pragma warning disable CS4014
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.UIExport.MenuExports
{
    public class ExportCameraLog : IMenuItem
    {
        public string? OwnerGuid => "ServiceLog";

        public string? GuidId => "CameraLog";

        public int Order => 1;
        public Visibility Visibility => Visibility.Visible;

        public string? Header => Resources.CameraLog;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
            PlatformHelper.OpenFolder("http://localhost:8064/system/device/camera/log");
        }
    }
}

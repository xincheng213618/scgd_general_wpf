using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (string.IsNullOrEmpty(status))
                return ColorVision.Engine.Properties.Resources.DeviceStatusUnknown;

            switch (status.ToLowerInvariant())
            {
                case "online":
                    return ColorVision.Engine.Properties.Resources.Online;
                case "offline":
                    return ColorVision.Engine.Properties.Resources.offline;
                case "unknown":
                    return ColorVision.Engine.Properties.Resources.DeviceStatusUnknown;
                default:
                    return status;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush OnlineBrush = CreateBrush("#FF2EAD4F");
        private static readonly SolidColorBrush OfflineBrush = CreateBrush("#FFE53935");
        private static readonly SolidColorBrush UnknownBrush = CreateBrush("#FF8A8F98");

        private static SolidColorBrush CreateBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string status = value as string;
            if (string.IsNullOrWhiteSpace(status))
                return UnknownBrush;

            return status.ToLowerInvariant() switch
            {
                "online" => OnlineBrush,
                "offline" => OfflineBrush,
                "unknown" => UnknownBrush,
                _ => UnknownBrush,
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PhyCameraOnlineComparer : IComparer
    {
        public int Compare(object? x, object? y)
        {
            if (x is PhyCamera a && y is PhyCamera b)
            {
                bool aOnline = string.Equals(a.SysResourceModel?.Remark, "Online", StringComparison.OrdinalIgnoreCase);
                bool bOnline = string.Equals(b.SysResourceModel?.Remark, "Online", StringComparison.OrdinalIgnoreCase);

                if (aOnline && !bOnline) return -1;
                if (!aOnline && bOnline) return 1;
                return a.Id.CompareTo(b.Id);
            }
            return 0;
        }
    }

    public class ExportPhyCamerManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => Properties.Resources.MenuPhyCameraManager;
        public override int Order => 2;

        public override void Execute()
        {
            new PhyCameraManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterScreen }.ShowDialog();
        }
    }

    public class PhyCameraManagerWindowConfig: WindowConfig
    {
        public static PhyCameraManagerWindowConfig Instance => ConfigService.Instance.GetRequiredService<PhyCameraManagerWindowConfig>();

        public bool AllowCreate { get => _AllowCreate; set { _AllowCreate = value; OnPropertyChanged(); } }
        private bool _AllowCreate;
    }

    /// <summary>
    /// PhySpectrumManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PhyCameraManagerWindow : Window
    {
        public static PhyCameraManagerWindowConfig Config  => ConfigService.Instance.GetRequiredService<PhyCameraManagerWindowConfig>();
        public PhyCameraManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            Config.SetWindow(this);
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            PhyCameraManager.GetInstance().LoadPhyCamera();
            this.DataContext = PhyCameraManager.GetInstance();
            PhyCameraManager.GetInstance().RefreshEmptyCamera();
            ApplyOnlineSorting();
            PhyCameraManager.GetInstance().PhyCameras.CollectionChanged += OnPhyCamerasCollectionChanged;
            this.Closed += (s, args) => PhyCameraManager.GetInstance().PhyCameras.CollectionChanged -= OnPhyCamerasCollectionChanged;

            CameraList.SelectedItem = PhyCameraManager.GetInstance().PhyCameras.FirstOrDefault(camera => camera.IsSelected)
                ?? PhyCameraManager.GetInstance().PhyCameras.FirstOrDefault();
            ShowSelectedCameraDetails();
        }

        private void OnPhyCamerasCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ApplyOnlineSorting();
            if (CameraList.SelectedItem == null)
            {
                CameraList.SelectedItem = PhyCameraManager.GetInstance().PhyCameras.FirstOrDefault();
            }
            ShowSelectedCameraDetails();
        }

        private static void ApplyOnlineSorting()
        {
            var view = CollectionViewSource.GetDefaultView(PhyCameraManager.GetInstance().PhyCameras);
            if (view is ListCollectionView listView)
            {
                listView.CustomSort = new PhyCameraOnlineComparer();
            }
        }

        private void CameraList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowSelectedCameraDetails();
        }

        private void ShowSelectedCameraDetails()
        {
            StackPanelShow.Children.Clear();
            if (CameraList.SelectedItem is PhyCamera phyCamera)
            {
                StackPanelShow.Children.Add(phyCamera.GetDeviceInfo());
            }
        }

        private void ToolbarMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
                return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }
}

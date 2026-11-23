using ColorVision.Engine.Services.Devices.Camera.Diagnostics;
using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras
{
    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                PhysicalCameraStatus.Opened => Properties.Resources.DeviceStatusOpened,
                PhysicalCameraStatus.Online => Properties.Resources.Online,
                PhysicalCameraStatus.Offline => Properties.Resources.offline,
                _ => Properties.Resources.DeviceStatusUnknown
            };
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
            return value switch
            {
                PhysicalCameraStatus.Opened or PhysicalCameraStatus.Online => OnlineBrush,
                PhysicalCameraStatus.Offline => OfflineBrush,
                _ => UnknownBrush
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
                bool aOnline = a.IsOnline;
                bool bOnline = b.IsOnline;

                if (aOnline && !bOnline) return -1;
                if (!aOnline && bOnline) return 1;
                return a.Id.CompareTo(b.Id);
            }
            return 0;
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
        private readonly CancellationTokenSource _statusScanCancellation = new();
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
            if (MvsDiagnosticsPaths.FindLogViewer(MvsDiagnosticsPaths.GetInstallationDirectories()) != null)
                MvsLogButton.Visibility = Visibility.Visible;
            ApplyOnlineSorting();
            PhyCameraManager.GetInstance().PhyCameras.CollectionChanged += OnPhyCamerasCollectionChanged;
            this.Closed += (s, args) => PhyCameraManager.GetInstance().PhyCameras.CollectionChanged -= OnPhyCamerasCollectionChanged;

            CameraList.SelectedItem = PhyCameraManager.GetInstance().PhyCameras.FirstOrDefault(camera => camera.IsSelected)
                ?? PhyCameraManager.GetInstance().PhyCameras.FirstOrDefault();
            ShowSelectedCameraDetails();
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            try
            {
                await PhyCameraManager.GetInstance().RefreshLocalCameraStatusAsync(_statusScanCancellation.Token);
            }
            catch (OperationCanceledException) when (_statusScanCancellation.IsCancellationRequested)
            {
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusScanCancellation.Cancel();
            _statusScanCancellation.Dispose();
            base.OnClosed(e);
        }

        private void OnPhyCamerasCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
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
                if (listView.CustomSort is PhyCameraOnlineComparer) return;
                listView.CustomSort = new PhyCameraOnlineComparer();
                listView.LiveSortingProperties.Add(nameof(PhyCamera.ConnectionStatus));
                listView.IsLiveSorting = true;
            }
        }

        private void CameraList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowSelectedCameraDetails();
        }

        private void ShowSelectedCameraDetails()
        {
            StackPanelShow.Children.Clear();
            EmptyState.Visibility = CameraList.SelectedItem is PhyCamera ? Visibility.Collapsed : Visibility.Visible;
            if (CameraList.SelectedItem is PhyCamera phyCamera)
            {
                StackPanelShow.Children.Add(phyCamera.GetDeviceInfo());
            }
        }
    }
}

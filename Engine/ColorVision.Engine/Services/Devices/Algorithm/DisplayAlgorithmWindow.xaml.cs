using ColorVision.Themes;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public partial class DisplayAlgorithmWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisplayAlgorithmWindow));
        private readonly DisplayAlgorithmMeta _meta;
        private readonly string _imageFilePath;
        private DeviceAlgorithm? _currentDevice;
        private IDisplayAlgorithm? _algorithm;
        private UserControl? _view;

        public DisplayAlgorithmWindow(
            DisplayAlgorithmMeta meta,
            IEnumerable<DeviceAlgorithm> devices,
            string? imageFilePath)
        {
            ArgumentNullException.ThrowIfNull(meta);
            ArgumentNullException.ThrowIfNull(devices);

            _meta = meta;
            _imageFilePath = imageFilePath ?? string.Empty;

            InitializeComponent();

            Title = meta.DisplayName;
            List<DeviceAlgorithm> deviceList = devices.ToList();
            DeviceSelector.ItemsSource = deviceList;
            DevicePanel.Visibility = deviceList.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            DeviceSelector.SelectedIndex = deviceList.Count == 1 ? 0 : -1;
            if (deviceList.Count > 1)
            {
                ShowStatus(CreateStatusText(Properties.Resources.SelectAlgorithmService));
            }
            Closed += DisplayAlgorithmWindow_Closed;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.ApplyCaption();
        }

        private void DeviceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentDevice != null)
            {
                _currentDevice.DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            }
            ReleaseView();

            if (DeviceSelector.SelectedItem is not DeviceAlgorithm device)
            {
                _currentDevice = null;
                ShowStatus(DeviceSelector.Items.Count > 1
                    ? CreateStatusText(Properties.Resources.SelectAlgorithmService)
                    : null);
                return;
            }

            _currentDevice = device;
            _currentDevice.DService.DeviceStatusChanged += DService_DeviceStatusChanged;
            RefreshAlgorithmContent();
        }

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            if (Dispatcher.HasShutdownStarted)
            {
                return;
            }

            if (Dispatcher.CheckAccess())
            {
                RefreshAlgorithmContent();
            }
            else
            {
                Dispatcher.BeginInvoke(RefreshAlgorithmContent);
            }
        }

        private void RefreshAlgorithmContent()
        {
            if (_currentDevice == null)
            {
                AlgorithmContent.Content = null;
                HideStatus();
                return;
            }

            if (_currentDevice.DService.DeviceStatus == DeviceStatusType.Unauthorized)
            {
                ShowStatus(CreateUnauthorizedButton(_currentDevice));
                return;
            }

            if (_currentDevice.DService.DeviceStatus == DeviceStatusType.Unknown)
            {
                ShowStatus(CreateStatusText(Properties.Resources.UnknownStatus));
                return;
            }

            if (_view != null)
            {
                AlgorithmContent.Content = _view;
                HideStatus();
                return;
            }

            try
            {
                DisplayAlgorithmManager manager = DisplayAlgorithmManager.GetInstance();
                _algorithm = manager.CreateAlgorithm(_meta.Type, _currentDevice, _imageFilePath);
                _view = manager.CreateView(_algorithm);
                AlgorithmContent.Content = _view;
                HideStatus();
            }
            catch (Exception ex)
            {
                _algorithm = null;
                _view = null;
                log.Error($"Could not create display algorithm window content for {_meta.Type.FullName}.", ex);
                AlgorithmContent.Content = null;
                ShowStatus(CreateStatusText(ex.Message));
            }
        }

        private void ShowStatus(object? content)
        {
            StatusContent.Content = content;
            bool isVisible = content != null;
            StatusOverlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            AlgorithmContent.IsEnabled = !isVisible;
        }

        private void HideStatus()
        {
            ShowStatus(null);
        }

        private static TextBlock CreateStatusText(string message)
        {
            return new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4)
            };
        }

        private static Button CreateUnauthorizedButton(DeviceAlgorithm device)
        {
            return new Button
            {
                Content = Properties.Resources.UnauthorizedOrLicenseExpired,
                Command = device.EditCommand,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(4)
            };
        }

        private void ReleaseView()
        {
            AlgorithmContent.Content = null;
            if (_view is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _algorithm = null;
            _view = null;
        }

        private void DisplayAlgorithmWindow_Closed(object? sender, EventArgs e)
        {
            if (_currentDevice != null)
            {
                _currentDevice.DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            }
            ReleaseView();
            HideStatus();
            Closed -= DisplayAlgorithmWindow_Closed;
        }
    }
}

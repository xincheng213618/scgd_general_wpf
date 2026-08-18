#pragma warning disable CA1304,CA1863
using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.Engine.PropertyEditor
{
    public class SerialPortModel : ViewModelBase
    {
        public string Name { get; set; }

        // 新增：用于显示总线概述或设备描述
        public string BusDescription { get => _BusDescription; set { _BusDescription = value; OnPropertyChanged(); } }
        private string _BusDescription;

        public string Status { get => _Status; set { _Status = value; OnPropertyChanged(); } }
        private string _Status;

        public Brush Color { get => _Color; set { _Color = value; OnPropertyChanged(); } }
        private Brush _Color;

        public string ErrorDetail { get => _ErrorDetail; set { _ErrorDetail = value; OnPropertyChanged(); } }
        private string _ErrorDetail;
    }

    public class TextSerialPortPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            Button btnRefresh = new Button
            {
                Content = GetResourceText("Refresh"),
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 50,
            };
            DockPanel.SetDock(btnRefresh, Dock.Right);
            dockPanel.Children.Add(btnRefresh);

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            var combo = new HandyControl.Controls.ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                IsEditable = true
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property));
            System.Windows.Controls.TextSearch.SetTextPath(combo, "Name");

            // UI 布局：水平 StackPanel
            DataTemplate itemTemplate = new DataTemplate();
            FrameworkElementFactory stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetBinding(FrameworkElement.ToolTipProperty, new Binding("ErrorDetail"));

            // 1. 端口号 (如 COM3)
            FrameworkElementFactory nameBlock = new FrameworkElementFactory(typeof(TextBlock));
            nameBlock.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            nameBlock.SetValue(TextBlock.WidthProperty, 60.0);
            nameBlock.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            stackPanelFactory.AppendChild(nameBlock);

            // 2. 详细描述 (如 BusDescription)
            FrameworkElementFactory descBlock = new FrameworkElementFactory(typeof(TextBlock));
            descBlock.SetBinding(TextBlock.TextProperty, new Binding("BusDescription"));
            descBlock.SetValue(TextBlock.WidthProperty, 150.0);
            descBlock.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
            descBlock.SetValue(TextBlock.MarginProperty, new Thickness(5, 0, 5, 0));
            // 如果描述过长，显示省略号
            descBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            stackPanelFactory.AppendChild(descBlock);

            // 3. 状态 (如 可用/占用)
            FrameworkElementFactory statusBlock = new FrameworkElementFactory(typeof(TextBlock));
            statusBlock.SetBinding(TextBlock.TextProperty, new Binding("Status"));
            statusBlock.SetBinding(TextBlock.ForegroundProperty, new Binding("Color"));
            stackPanelFactory.AppendChild(statusBlock);

            itemTemplate.VisualTree = stackPanelFactory;
            combo.ItemTemplate = itemTemplate;

            dockPanel.Children.Add(combo);

            CancellationTokenSource? refreshCancellation = null;

            void RefreshPorts(bool probeAvailability)
            {
                refreshCancellation?.Cancel();
                refreshCancellation?.Dispose();
                refreshCancellation = new CancellationTokenSource();
                CancellationToken cancellationToken = refreshCancellation.Token;

                // 使用我们编写好的底层 API 获取详细的设备列表
                List<Win32DeviceMgmt.DeviceInfo> devices = new List<Win32DeviceMgmt.DeviceInfo>();
                try
                {
                    devices = Win32DeviceMgmt.GetAllCOMPorts();
                }
                catch
                {
                    // 降级处理：如果底层获取失败，回退到原生的 GetPortNames
                    foreach (var p in SerialPort.GetPortNames())
                    {
                        devices.Add(new Win32DeviceMgmt.DeviceInfo { name = p, description = GetResourceText("SerialPortUnknownDevice"), bus_description = "" });
                    }
                }

                var initialModels = devices.Select(d => new SerialPortModel
                {
                    Name = d.name,
                    // 优先显示 BusDescription，如果没有则显示 Description
                    BusDescription = !string.IsNullOrWhiteSpace(d.bus_description) ? d.bus_description : d.description,
                    Status = probeAvailability ? GetResourceText("SerialPortChecking") : string.Empty,
                    Color = Brushes.Gray,
                    ErrorDetail = probeAvailability ? GetResourceText("SerialPortCheckingDetail") : string.Empty
                }).ToList();

                combo.ItemsSource = initialModels;

                if (!probeAvailability)
                    return;

                _ = Task.Run(() =>
                {
                    foreach (var model in initialModels)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        SerialPortProbeResult result = ProbePort(model.Name);
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        dockPanel.Dispatcher.BeginInvoke(() =>
                        {
                            if (cancellationToken.IsCancellationRequested || !ReferenceEquals(combo.ItemsSource, initialModels))
                                return;

                            model.Status = result.Status;
                            model.Color = result.Color;
                            model.ErrorDetail = result.ErrorDetail;
                        });
                    }
                }, cancellationToken);
            }

            btnRefresh.Click += (_, _) => RefreshPorts(probeAvailability: true);
            dockPanel.Unloaded += (_, _) => refreshCancellation?.Cancel();
            RefreshPorts(probeAvailability: false);

            return dockPanel;
        }

        private static string GetResourceText(string key)
        {
            return ColorVision.Engine.Properties.Resources.ResourceManager.GetString(key) ?? key;
        }

        private static SerialPortProbeResult ProbePort(string portName)
        {
            try
            {
                using (SerialPort serialPort = new SerialPort(portName))
                {
                    serialPort.Open();
                    return new SerialPortProbeResult(
                        GetResourceText("SerialPortAvailable"),
                        Brushes.Green,
                        GetResourceText("SerialPortAvailableDetail"));
                }
            }
            catch (UnauthorizedAccessException)
            {
                return new SerialPortProbeResult(
                    GetResourceText("SerialPortOccupiedShort"),
                    Brushes.Red,
                    GetResourceText("SerialPortOccupiedDetail"));
            }
            catch (Exception ex)
            {
                return new SerialPortProbeResult(
                    GetResourceText("SerialPortErrorShort"),
                    Brushes.Orange,
                    string.Format(GetResourceText("SerialPortOpenFailedDetail"), ex.Message));
            }
        }

        private sealed record SerialPortProbeResult(string Status, Brush Color, string ErrorDetail);
    }
}

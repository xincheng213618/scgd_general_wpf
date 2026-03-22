using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
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
                Content = "刷新",
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
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
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

            void RefreshPorts()
            {
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
                        devices.Add(new Win32DeviceMgmt.DeviceInfo { name = p, description = "未知设备", bus_description = "" });
                    }
                }

                var initialModels = devices.Select(d => new SerialPortModel
                {
                    Name = d.name,
                    // 优先显示 BusDescription，如果没有则显示 Description
                    BusDescription = !string.IsNullOrWhiteSpace(d.bus_description) ? d.bus_description : d.description,
                    Status = "检测中...",
                    Color = Brushes.Gray,
                    ErrorDetail = "正在检测端口状态..."
                }).ToList();

                combo.ItemsSource = initialModels;

                Task.Run(() =>
                {
                    foreach (var model in initialModels)
                    {
                        CheckPortStatus(model);
                    }
                });
            }

            btnRefresh.Click += (s, e) => RefreshPorts();
            RefreshPorts();

            return dockPanel;
        }

        private void CheckPortStatus(SerialPortModel model)
        {
            try
            {
                using (SerialPort serialPort = new SerialPort(model.Name))
                {
                    serialPort.Open();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        model.Status = "可用";
                        model.Color = Brushes.Green;
                        model.ErrorDetail = "端口空闲，可直接连接";
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    model.Status = "占用";
                    model.Color = Brushes.Red;
                    model.ErrorDetail = "端口已被其他程序占用 (Unauthorized Access)";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    model.Status = "异常";
                    model.Color = Brushes.Orange;
                    model.ErrorDetail = $"无法打开端口: {ex.Message}";
                });
            }
        }
    }
}
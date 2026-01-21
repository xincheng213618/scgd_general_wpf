using ColorVision.Common.MVVM;
using ColorVision.UI;
using ICSharpCode.AvalonEdit.Document;
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

        public string Status { get => _Status; set { _Status = value; OnPropertyChanged(); } }
        private string _Status;

        public Brush Color { get => _Color; set { _Color = value; OnPropertyChanged(); } }
        private Brush _Color;

        // 新增：用于显示详细错误信息的 ToolTip
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

            DataTemplate itemTemplate = new DataTemplate();
            FrameworkElementFactory stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // 给整个条目添加 ToolTip
            stackPanelFactory.SetBinding(FrameworkElement.ToolTipProperty, new Binding("ErrorDetail"));

            FrameworkElementFactory nameBlock = new FrameworkElementFactory(typeof(TextBlock));
            nameBlock.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            nameBlock.SetValue(TextBlock.WidthProperty, 70.0);
            stackPanelFactory.AppendChild(nameBlock);

            FrameworkElementFactory statusBlock = new FrameworkElementFactory(typeof(TextBlock));
            statusBlock.SetBinding(TextBlock.TextProperty, new Binding("Status"));
            statusBlock.SetBinding(TextBlock.ForegroundProperty, new Binding("Color"));
            stackPanelFactory.AppendChild(statusBlock);

            itemTemplate.VisualTree = stackPanelFactory;
            combo.ItemTemplate = itemTemplate;

            dockPanel.Children.Add(combo);

            void RefreshPorts()
            {
                string[] portNames = SerialPort.GetPortNames();

                var initialModels = portNames.Select(p => new SerialPortModel
                {
                    Name = p,
                    Status = "可用",
                    Color = Brushes.Green,
                    ErrorDetail = "端口状态正常"
                }).ToList();

                combo.ItemsSource = initialModels;

                Task.Run(() =>
                {
                    foreach (var model in initialModels)
                    {
                        // 传入 model 以便更新详细错误信息
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
                    // 成功打开，不需要做任何改变，默认就是绿色可用
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
                // 这是最常见的“被占用”
                Application.Current.Dispatcher.Invoke(() =>
                {
                    model.Status = "占用";
                    model.Color = Brushes.Red;
                    model.ErrorDetail = "端口已被其他程序占用 (Unauthorized Access)";
                });
            }
            catch (Exception ex)
            {
                // 其他错误（如驱动问题、参数错误等）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    model.Status = "异常";
                    model.Color = Brushes.Orange; // 用橙色区分未知错误
                    model.ErrorDetail = $"无法打开端口: {ex.Message}";
                });
            }
        }
    }
}
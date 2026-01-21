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

        // 支持动态更新通知
        public string Status { get => _Status; set { _Status = value; OnPropertyChanged(); } }
        private string _Status;

        public Brush Color { get => _Color; set { _Color = value; OnPropertyChanged(); } }
        private Brush _Color;
    }

    public class TextSerialPortPropertiesEditor : IPropertyEditor
    {
        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();

            // 1. 增加刷新按钮
            Button btnRefresh = new Button
            {
                Content = "刷新", // 或者使用图标
                Margin = new Thickness(5, 0, 0, 0),
                MinWidth = 50,
            };
            DockPanel.SetDock(btnRefresh, Dock.Right);
            dockPanel.Children.Add(btnRefresh);

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);

            // 2. 初始化 ComboBox
            var combo = new HandyControl.Controls.ComboBox
            {
                Margin = new Thickness(5, 0, 0, 0),
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                IsEditable = true
            };
            HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
            combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
            System.Windows.Controls.TextSearch.SetTextPath(combo, "Name");

            // 3. 设置 ItemTemplate (显示 端口名 + 状态)
            DataTemplate itemTemplate = new DataTemplate();
            FrameworkElementFactory stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // 端口名
            FrameworkElementFactory nameBlock = new FrameworkElementFactory(typeof(TextBlock));
            nameBlock.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            nameBlock.SetValue(TextBlock.WidthProperty, 70.0);
            stackPanelFactory.AppendChild(nameBlock);

            // 状态文字 (带颜色)
            FrameworkElementFactory statusBlock = new FrameworkElementFactory(typeof(TextBlock));
            statusBlock.SetBinding(TextBlock.TextProperty, new Binding("Status"));
            statusBlock.SetBinding(TextBlock.ForegroundProperty, new Binding("Color"));
            stackPanelFactory.AppendChild(statusBlock);

            itemTemplate.VisualTree = stackPanelFactory;
            combo.ItemTemplate = itemTemplate;

            dockPanel.Children.Add(combo);

            // 4. 定义刷新逻辑 (异步)
            void RefreshPorts()
            {
                // 先获取端口列表
                string[] portNames = SerialPort.GetPortNames();

                // 先在 UI 上显示列表，状态设为 "检测中..." (灰色)
                var initialModels = portNames.Select(p => new SerialPortModel
                {
                    Name = p,
                    Status = "检测中...",
                    Color = Brushes.Gray
                }).ToList();

                combo.ItemsSource = initialModels;

                // 开启后台线程进行耗时的 Open 检测
                Task.Run(() =>
                {
                    // 为了避免修改 ItemsSource 集合带来的线程问题，我们创建一个新列表或者直接更新 Model 属性
                    // 这里选择直接更新 Model 属性，因为 ViewModelBase 支持通知
                    foreach (var model in initialModels)
                    {
                        bool isOccupied = IsPortOccupied(model.Name);

                        // 回到 UI 线程更新单个 Item 的状态 (虽然 ViewModel 属性变更不需要强制回到 UI 线程，但为了安全起见)
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (isOccupied)
                            {
                                model.Status = "占用";
                                model.Color = Brushes.Red;
                            }
                            else
                            {
                                model.Status = "可用";
                                model.Color = Brushes.Green;
                            }
                        });
                    }
                });
            }

            // 绑定按钮事件
            btnRefresh.Click += (s, e) => RefreshPorts();

            // 初始加载一次
            RefreshPorts();

            return dockPanel;
        }

        private bool IsPortOccupied(string portName)
        {
            try
            {
                using (SerialPort serialPort = new SerialPort(portName))
                {
                    serialPort.Open();
                    return false; // 能打开，说明未被占用（可用）
                }
            }
            catch
            {
                return true; // 打开失败，说明被占用���不可用
            }
        }
    }
}
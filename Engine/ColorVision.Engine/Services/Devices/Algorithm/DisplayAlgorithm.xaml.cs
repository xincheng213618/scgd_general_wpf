using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class DisplayAlgorithmMeta
    {
        public Type Type { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
    }

    public class DisplayAlgorithmConfig: IDisPlayConfigBase
    {
        public string LastSelectTemplate { get => _LastSelectTemplate; set { _LastSelectTemplate = value; OnPropertyChanged(); } }
        private string _LastSelectTemplate = "POI";

        public string LastSelectGroup { get => _LastSelectGroup; set { _LastSelectGroup = value; OnPropertyChanged(); } }
        private string _LastSelectGroup = "All";
    }


    /// <summary>
    /// DisplayAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayAlgorithm : UserControl,IDisPlayControl
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayAlgorithm));
        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm Service { get => Device.DService; }
        public string DisPlayName => Device.Config.Name;
        public DisplayAlgorithm(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();
        }

        public static FrameworkElement FindChildByName(DependencyObject parent, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe)
                {
                    if (fe.Name == name) return fe;
                    var result = FindChildByName(fe, name);
                    if (result != null) return result;
                }
            }
            return null;
        }
        Dictionary<Type, IDisplayAlgorithm> AlgorithmDict = new Dictionary<Type, IDisplayAlgorithm>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;

            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = Device.PropertyCommand });
            List<DisplayAlgorithmMeta> algorithmMetas = new List<DisplayAlgorithmMeta>();

            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IDisplayAlgorithm).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    var attr = type.GetCustomAttribute<DisplayAlgorithmAttribute>();
                    if (attr != null)
                    {
                        var meta = new DisplayAlgorithmMeta
                        {
                            Type = type,
                            Order = attr.Order,
                            Name = attr.Name,
                            Group = attr.Group
                        };

                        algorithmMetas.Add(meta);

                        if (Activator.CreateInstance(meta.Type, Device) is IDisplayAlgorithm  algorithm)
                        {
                            AlgorithmDict[meta.Type] = algorithm;
                        }
                    }
                }
            }
            // 用于展示和分组（不需要实例化对象）
            string allAlgorithmsGroup = "All";
            var groups = new List<string> { allAlgorithmsGroup };
            groups.AddRange(algorithmMetas.Select(a => a.Group).Distinct().Where(g => !string.IsNullOrWhiteSpace(g) && g != allAlgorithmsGroup));

            CB_AlgorithmTypes.ItemsSource = groups;
            CB_AlgorithmTypes.SelectedItem = Device.DisplayConfig.LastSelectGroup;

            // 按分组和排序展示算法
            var filteredAlgorithms = algorithmMetas
                .Where(a => a.Group == (string)CB_AlgorithmTypes.SelectedItem || (string)CB_AlgorithmTypes.SelectedItem == allAlgorithmsGroup)
                .OrderBy(a => a.Order)
                .ToList();

            CB_Algorithms.ItemsSource = filteredAlgorithms;
            CB_Algorithms.DisplayMemberPath = "Name";  // 假设绑定到 Name 显示
            CB_Algorithms.SelectionChanged += (s, e) =>
            {
                if (CB_Algorithms.SelectedItem is DisplayAlgorithmMeta meta)
                {
                    IDisplayAlgorithm algorithm;
                    if (!AlgorithmDict.TryGetValue(meta.Type, out algorithm))
                    {
                        algorithm = Activator.CreateInstance(meta.Type, Device) as IDisplayAlgorithm;
                        if (algorithm != null)
                        {
                            AlgorithmDict[meta.Type] = algorithm;
                        }
                        else
                        {
                            // 可选：异常处理或日志
                            return;
                        }
                    }

                    Device.DisplayConfig.LastSelectTemplate = meta.Name;
                    CB_StackPanel.Children.Clear();
                    CB_StackPanel.Children.Add(algorithm.GetUserControl());

                }
            };

            DisplayAlgorithmManager.GetInstance().SelectParamChanged += (s,e) =>
            {
                if (AlgorithmDict.TryGetValue(e.Type, out IDisplayAlgorithm algorithm))
                {
                    CB_AlgorithmTypes.SelectedItem = "All";
                    CB_Algorithms.SelectedItem = algorithmMetas.FirstOrDefault(a=>a.Type == e.Type);
                    algorithm.IsLocalFile = true;
                    algorithm.ImageFilePath = e.ImageFilePath ?? string.Empty;
                }

            };

            void CB_AlgorithmTypesChanged()
            {
                if (CB_AlgorithmTypes.SelectedItem is string selectedGroup)
                {
                    Device.DisplayConfig.LastSelectGroup = selectedGroup;
                    List<DisplayAlgorithmMeta> filteredAlgorithms;
                    if (selectedGroup == allAlgorithmsGroup)
                    {
                        filteredAlgorithms = algorithmMetas
                            .OrderBy(a => a.Order)
                            .ToList();
                    }
                    else
                    {
                        filteredAlgorithms = algorithmMetas
                            .Where(a => a.Group == selectedGroup)
                            .OrderBy(a => a.Order)
                            .ToList();
                    }

                    CB_Algorithms.ItemsSource = filteredAlgorithms;
                    CB_Algorithms.DisplayMemberPath = "Name";

                    var lastSelectedAlgorithm = filteredAlgorithms
                        .FirstOrDefault(a => a.Name == Device.DisplayConfig.LastSelectTemplate);

                    if (lastSelectedAlgorithm != null)
                    {
                        CB_Algorithms.SelectedItem = lastSelectedAlgorithm;
                    }
                    else
                    {
                        CB_Algorithms.SelectedIndex = 0;
                    }


                }
            }

            // 更新 CB_Algorithms 的绑定
            CB_AlgorithmTypes.SelectionChanged += (s, e) => CB_AlgorithmTypesChanged();
            CB_AlgorithmTypesChanged();




            // 默认选中 "All Algorithms" 组


            this.AddViewConfig(Device.View, ComboxView);
            this.ApplyChangedSelectedColor(DisPlayBorder);


            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility){ if (element.Visibility != visibility) element.Visibility = visibility; };
                void HideAllButtons()
                {
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                }
                // Default state
                HideAllButtons();

                switch (status)
                {
                    case DeviceStatusType.Unauthorized:
                        SetVisibility(ButtonUnauthorized, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unknown:
                        SetVisibility(TextBlockUnknow, Visibility.Visible);
                        break;
                    default:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        break;
                }
            }
            UpdateUI(Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += UpdateUI;
        }


        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }
        private bool _IsSelected;

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked = !ToggleButton0.IsChecked;
        }
    }
}

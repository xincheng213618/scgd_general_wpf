#pragma warning disable CA1816,CS8603
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class DisplayAlgorithmConfig : IDisplayConfigBase
    {
        public string LastSelectTemplate { get => _lastSelectTemplate; set { _lastSelectTemplate = value; OnPropertyChanged(); } }
        private string _lastSelectTemplate = "POI";

        public string LastSelectGroup { get => _lastSelectGroup; set { _lastSelectGroup = value; OnPropertyChanged(); } }
        private string _lastSelectGroup = "All";
    }

    /// <summary>
    /// DisplayAlgorithm.xaml 的交互逻辑
    /// </summary>
    public partial class DisplayAlgorithm : UserControl, IDisPlayControl, IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(DisplayAlgorithm));

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm Service => Device.DService;
        public string DisPlayName => Device.Config.Name;

        private readonly Dictionary<Type, IDisplayAlgorithm> _algorithmDict = new();
        private readonly Dictionary<Type, UserControl> _algorithmViewDict = new();
        private List<DisplayAlgorithmMeta> _algorithmMetas = new();
        private DisplayAlgorithmManager? _algorithmManager;
        private readonly string _allAlgorithmsGroup = "All";

        public DisplayAlgorithm(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();
        }

        public static FrameworkElement? FindChildByName(DependencyObject parent, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement element)
                {
                    if (element.Name == name)
                    {
                        return element;
                    }

                    FrameworkElement? result = FindChildByName(element, name);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            _algorithmManager = DisplayAlgorithmManager.GetInstance();
            _algorithmMetas = _algorithmManager.AlgorithmMetas.OrderBy(a => a.Order).ToList();

            CB_Algorithms.SelectionChanged += (_, _) =>
            {
                if (CB_Algorithms.SelectedItem is not DisplayAlgorithmMeta meta)
                {
                    return;
                }

                if (!_algorithmDict.TryGetValue(meta.Type, out IDisplayAlgorithm? algorithm))
                {
                    algorithm = _algorithmManager.CreateAlgorithm(meta.Type, Device);
                    _algorithmDict[meta.Type] = algorithm;
                }

                Device.DisplayConfig.LastSelectTemplate = meta.Name;
                if (!_algorithmViewDict.TryGetValue(meta.Type, out UserControl? view))
                {
                    view = _algorithmManager.CreateView(algorithm);
                    _algorithmViewDict[meta.Type] = view;
                }

                CB_StackPanel.Children.Clear();
                CB_StackPanel.Children.Add(view);
            };

            InitializeAlgorithmList();
            CB_AlgorithmTypes.SelectionChanged += (_, _) => CB_AlgorithmTypesChanged();

            this.AddViewConfig(Device.View, DisPlayName);
            this.ApplyChangedSelectedColor(DisPlayBorder);

            UpdateUI(Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += DService_DeviceStatusChanged;
        }

        private void InitializeAlgorithmList()
        {
            List<string> groups = new() { _allAlgorithmsGroup };
            groups.AddRange(_algorithmMetas
                .Select(a => a.Group)
                .Distinct()
                .Where(group => !string.IsNullOrWhiteSpace(group) && group != _allAlgorithmsGroup));

            string previousGroup = CB_AlgorithmTypes.SelectedItem as string ?? Device.DisplayConfig.LastSelectGroup;
            CB_AlgorithmTypes.ItemsSource = groups;
            CB_AlgorithmTypes.SelectedItem = groups.Contains(previousGroup)
                ? previousGroup
                : _allAlgorithmsGroup;

            CB_AlgorithmTypesChanged();
        }

        private void CB_AlgorithmTypesChanged()
        {
            if (CB_AlgorithmTypes.SelectedItem is not string selectedGroup)
            {
                return;
            }

            Device.DisplayConfig.LastSelectGroup = selectedGroup;
            List<DisplayAlgorithmMeta> filteredAlgorithms = selectedGroup == _allAlgorithmsGroup
                ? _algorithmMetas
                : _algorithmMetas
                    .Where(a => a.Group == selectedGroup)
                    .ToList();

            CB_Algorithms.ItemsSource = filteredAlgorithms;
            CB_Algorithms.DisplayMemberPath = nameof(DisplayAlgorithmMeta.DisplayName);

            DisplayAlgorithmMeta? lastSelectedAlgorithm = filteredAlgorithms
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

        private void UpdateUI(DeviceStatusType status)
        {
            static void SetVisibility(UIElement element, Visibility visibility)
            {
                if (element.Visibility != visibility)
                {
                    element.Visibility = visibility;
                }
            }

            SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
            SetVisibility(TextBlockUnknow, Visibility.Collapsed);
            SetVisibility(StackPanelContent, Visibility.Collapsed);

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

        private void DService_DeviceStatusChanged(object? sender, DeviceStatusType e)
        {
            UpdateUI(e);
        }

        public event RoutedEventHandler? Selected;
        public event RoutedEventHandler? Unselected;
        public event EventHandler? SelectChanged;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                SelectChanged?.Invoke(this, new RoutedEventArgs());
                if (value)
                {
                    Selected?.Invoke(this, new RoutedEventArgs());
                }
                else
                {
                    Unselected?.Invoke(this, new RoutedEventArgs());
                }
            }
        }
        private bool _isSelected;

        public void Dispose()
        {
            Device.DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
        }
    }
}

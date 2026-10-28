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
        private bool _isInitialized;
        private bool _isDisposed;

        public DeviceAlgorithm Device { get; set; }
        public MQTTAlgorithm Service => Device.DService;
        public string DisPlayName => Device.Config.Name;
        public string PersistenceKey => Device.Config.Code;

        private readonly Dictionary<Type, IDisplayAlgorithm> _algorithmDict = new();
        private readonly Dictionary<Type, UserControl> _algorithmViewDict = new();
        private List<DisplayAlgorithmMeta> _algorithmMetas = new();
        private DisplayAlgorithmManager? _algorithmManager;

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
            if (_isInitialized || _isDisposed)
                return;

            _isInitialized = true;
            DataContext = Device;
            _algorithmManager = DisplayAlgorithmManager.GetInstance();
            _algorithmMetas = _algorithmManager.AlgorithmMetas.OrderBy(a => a.Order).ToList();

            CB_Algorithms.SelectionChanged += CB_Algorithms_SelectionChanged;
            InitializeAlgorithmList();

            this.AddViewConfig(Device.ViewRegistration, DisPlayName);
            this.ApplyChangedSelectedColor(DisPlayBorder);

            UpdateUI(Device.DService.DeviceStatus);
            Device.DService.DeviceStatusChanged += DService_DeviceStatusChanged;
        }

        private void CB_Algorithms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isDisposed || _algorithmManager == null || CB_Algorithms.SelectedItem is not DisplayAlgorithmMeta meta)
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
            CalculateAction.Content = null;
            if (view is DisplayAlgorithmControl control)
            {
                CalculateAction.Content = control.DetachCalculateButton();
            }
        }

        private void InitializeAlgorithmList()
        {
            CB_Algorithms.ItemsSource = _algorithmMetas;
            CB_Algorithms.DisplayMemberPath = nameof(DisplayAlgorithmMeta.DisplayName);

            DisplayAlgorithmMeta? lastSelectedAlgorithm = _algorithmMetas
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
            if (_isDisposed)
                return;

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
            if (_isDisposed)
                return;

            _isDisposed = true;
            Device.DService.DeviceStatusChanged -= DService_DeviceStatusChanged;
            CB_Algorithms.SelectionChanged -= CB_Algorithms_SelectionChanged;

            CB_Algorithms.ItemsSource = null;
            CalculateAction.Content = null;
            CB_StackPanel.Children.Clear();

            _algorithmViewDict.Clear();
            _algorithmDict.Clear();
            _algorithmMetas.Clear();
            _algorithmManager = null;
            DataContext = null;
            GC.SuppressFinalize(this);
        }
    }
}

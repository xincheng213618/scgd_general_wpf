using ColorVision.Common.MVVM;
using ColorVision.Engine.Interfaces;
using ColorVision.UI;
using CVCommCore;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public class DisplayAlgorithmConfig:ViewModelBase,IConfig
    {
        public static DisplayAlgorithmConfig Instance => ConfigService.Instance.GetRequiredService<DisplayAlgorithmConfig>();

        public string LastSelectTemplate { get => _LastSelectTemplate; set { _LastSelectTemplate = value; NotifyPropertyChanged(); } }
        private string _LastSelectTemplate;

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
        public ObservableCollection<IDisplayAlgorithm> Algorithms { get; set; } 
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            List<IDisplayAlgorithm> algorithms = new List<IDisplayAlgorithm>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IDisplayAlgorithm).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type, Device) is IDisplayAlgorithm  algorithm)
                    {
                        algorithms.Add(algorithm);
                    }
                }
            }

            Algorithms = new ObservableCollection<IDisplayAlgorithm>(algorithms.OrderBy(item => item.Order));
            CB_Algorithms.ItemsSource = Algorithms;

            CB_Algorithms.SelectionChanged += (s, e) =>
            {
                if (CB_Algorithms.SelectedItem is IDisplayAlgorithm algorithm)
                {
                    DisplayAlgorithmConfig.Instance.LastSelectTemplate = algorithm.Name;
                    CB_StackPanel.Children.Clear();
                    CB_StackPanel.Children.Add(algorithm.GetUserControl());
                }
            };

            var lastSelectedAlgorithm = Algorithms
                .FirstOrDefault(a => a.Name == DisplayAlgorithmConfig.Instance.LastSelectTemplate);

            if (lastSelectedAlgorithm != null)
            {
                CB_Algorithms.SelectedIndex = Algorithms.IndexOf(lastSelectedAlgorithm);
            }
            else
            {
                CB_Algorithms.SelectedIndex = 0; // Default to the first item if no match is found
            }
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

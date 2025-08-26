using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// SpectrumResourceControl.xaml 的交互逻辑
    /// </summary>
    public partial class SpectrumResourceControl : UserControl
    {

        public SpectrumResourceParam SpectrumResourceParam { get; set; }
        public DeviceSpectrum DeviceSpectrum { get; set; }


        public SpectrumResourceControl(DeviceSpectrum deviceSpectrum)
        {
            DeviceSpectrum = deviceSpectrum;
            InitializeComponent();
        }

        public SpectrumResourceControl(DeviceSpectrum  deviceSpectrum,SpectrumResourceParam spectrumResourceParam)
        {
            DeviceSpectrum = deviceSpectrum;
            SpectrumResourceParam = spectrumResourceParam;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = SpectrumResourceParam;
            ComboBoxList.ItemsSource = BaseResources;

        }
        public ObservableCollection<ServiceBase> BaseResources { get; set; } = new ObservableCollection<ServiceBase>();

        public void Initializedsss(DeviceSpectrum deviceSpectrum, SpectrumResourceParam spectrumResourceParam)
        {
            ComboBoxList.SelectionChanged -= ComboBox_SelectionChanged;
            DeviceSpectrum = deviceSpectrum;
            SpectrumResourceParam = spectrumResourceParam;
            DataContext = SpectrumResourceParam;

            string CalibrationMode = SpectrumResourceParam.ResourceMode;

            BaseResources.Clear();
            foreach (var item in deviceSpectrum.VisualChildren)
            {
                if (item is ServiceBase baseResources)
                {
                    BaseResources.Add(baseResources);
                }
            }
            ComboBoxList.Text = CalibrationMode;
            ComboBoxList.SelectionChanged += ComboBox_SelectionChanged;

        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                SpectrumResourceParam.IsSelected = false;

                if (comboBox.SelectedValue is ServiceBase baseResource)
                {
                    SpectrumResourceParam.ResourceName = baseResource.Name;
                    SpectrumResourceParam.ResourceId = baseResource.Id;
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

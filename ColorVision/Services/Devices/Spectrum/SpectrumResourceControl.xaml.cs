using ColorVision.Services.Devices.Camera;
using ColorVision.Services.Devices.Camera.Calibrations;
using ColorVision.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision.Services.Devices.Spectrum
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
            this.DataContext = SpectrumResourceParam;
            ComboBoxList.ItemsSource = BaseResources;

        }
        public ObservableCollection<BaseResource> BaseResources { get; set; } = new ObservableCollection<BaseResource>();

        public void Initializedsss(DeviceSpectrum deviceSpectrum, SpectrumResourceParam spectrumResourceParam)
        {
            DeviceSpectrum = deviceSpectrum;
            SpectrumResourceParam = spectrumResourceParam;
            this.DataContext = SpectrumResourceParam;

            string CalibrationMode = SpectrumResourceParam.ResourceMode;
            ComboBoxList.SelectionChanged -= ComboBox_SelectionChanged;
            BaseResources.Clear();
            foreach (var item in deviceSpectrum.VisualChildren)
            {
                if (item is BaseResource baseResources)
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

                if (comboBox.SelectedValue is BaseResource baseResource)
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

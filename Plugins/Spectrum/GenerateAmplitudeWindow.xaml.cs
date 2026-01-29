using System.Windows;

namespace Spectrum
{
    /// <summary>
    /// GenerateAmplitudeWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GenerateAmplitudeWindow : Window
    {
        public IntPtr SpectrometerHandle { get; set; } = IntPtr.Zero;


        public GenerateAmplitudeWindow(IntPtr spectrometerHandle)
        {
            SpectrometerHandle = spectrometerHandle;
            InitializeComponent();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            SpectrometerManager generateAmplitudeParam = new SpectrometerManager();
            generateAmplitudeParam.Handle = SpectrometerHandle;
            this.DataContext = generateAmplitudeParam;
        }
    }
}

using ColorVision.Engine.Messages;
using ColorVision.Themes.Controls;
using log4net;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public partial class DisplayAlgorithmControl : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisplayAlgorithmControl));
        private readonly IDisplayAlgorithm _algorithm;

        public DisplayAlgorithmControl(IDisplayAlgorithm algorithm)
        {
            _algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            InitializeComponent();
            ConfigurationContent.Content = new DisplayAlgorithmConfigurationBuilder().Build(algorithm.Configuration);
        }

        internal Button DetachCalculateButton()
        {
            CalculateButtonHost.Content = null;
            CalculateButtonHost.Visibility = Visibility.Collapsed;
            return CalculateButton;
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MsgRecord? msgRecord = _algorithm.Execute();
                if (msgRecord != null)
                {
                    ServicesHelper.SendCommand(CalculateButton, msgRecord);
                }
            }
            catch (Exception ex)
            {
                log.Error($"Could not execute display algorithm {_algorithm.GetType().FullName}.", ex);
                MessageBox1.Show(
                    Window.GetWindow(this),
                    ex.Message,
                    "ColorVision");
            }
        }
    }
}

using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
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
        private readonly Button _calculateButton;

        public DisplayAlgorithmControl(IDisplayAlgorithm algorithm)
        {
            _algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
            InitializeComponent();
            _calculateButton = new Button
            {
                Content = Properties.Resources.Calculate
            };
            _calculateButton.Click += CalculateButton_Click;
            ConfigurationContent.Content = new DisplayAlgorithmConfigurationBuilder().Build(
                algorithm.Configuration,
                _calculateButton);
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MsgRecord? msgRecord = _algorithm.Execute();
                if (msgRecord != null)
                {
                    ServicesHelper.SendCommand(_calculateButton, msgRecord);
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

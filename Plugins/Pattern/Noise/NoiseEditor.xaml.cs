using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Noise
{
    /// <summary>
    /// NoiseEditor.xaml 的交互逻辑
    /// </summary>
    public partial class NoiseEditor : UserControl
    {
        public PatternNoiseConfig Config { get; set; }

        public NoiseEditor(PatternNoiseConfig config)
        {
            Config = config;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }

        private void BtnPickBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectBackgroundColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                Config.BackgroundBrush = ColorPicker1.SelectedBrush;
            };
            Window window = new Window() 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner, 
                Content = ColorPicker1, 
                Width = 250, 
                Height = 400 
            };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.BackgroundBrush = ColorPicker1.SelectedBrush;
                window.Close();
            };
            window.Closed += (s, e) =>
            {
                ColorPicker1.Dispose();
            };
            window.Show();
        }

        private void BtnPickBackgroundColorSet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag.ToString();
                Config.BackgroundBrush = tag switch
                {
                    "R" => Brushes.Red,
                    "G" => Brushes.Lime,
                    "B" => Brushes.Blue,
                    "W" => Brushes.White,
                    "K" => Brushes.Black,
                    _ => Config.BackgroundBrush
                };
            }
        }

        private void BtnPickNoiseColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectNoiseColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                Config.NoiseBrush = ColorPicker1.SelectedBrush;
            };
            Window window = new Window() 
            { 
                Owner = Application.Current.GetActiveWindow(), 
                WindowStartupLocation = WindowStartupLocation.CenterOwner, 
                Content = ColorPicker1, 
                Width = 250, 
                Height = 400 
            };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.NoiseBrush = ColorPicker1.SelectedBrush;
                window.Close();
            };
            window.Closed += (s, e) =>
            {
                ColorPicker1.Dispose();
            };
            window.Show();
        }

        private void BtnPickNoiseColorSet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag.ToString();
                Config.NoiseBrush = tag switch
                {
                    "R" => Brushes.Red,
                    "G" => Brushes.Lime,
                    "B" => Brushes.Blue,
                    "W" => Brushes.White,
                    "K" => Brushes.Black,
                    _ => Config.NoiseBrush
                };
            }
        }
    }
}

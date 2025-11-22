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
            
            // Initialize noise type combo box with Description attributes
            cmbNoiseType.ItemsSource = from e1 in Enum.GetValues(typeof(NoiseType)).Cast<NoiseType>()
                                       select new KeyValuePair<NoiseType, string>(e1, e1.ToDescription());

            // Initialize size mode combo box
            cmbSizeMode.ItemsSource = from e1 in Enum.GetValues(typeof(SolidSizeMode)).Cast<SolidSizeMode>()
                                      select new KeyValuePair<SolidSizeMode, string>(e1, e1.ToString());
            
            UpdateNoiseTypeVisibility();
            UpdateSizeModeVisibility();
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

        private void cmbNoiseType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateNoiseTypeVisibility();
        }

        private void cmbSizeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSizeModeVisibility();
        }

        private void UpdateNoiseTypeVisibility()
        {
            if (cmbNoiseType?.SelectedValue is NoiseType noiseType)
            {
                // Show/hide intensity panel based on noise type
                if (noiseType == NoiseType.Gaussian || noiseType == NoiseType.Uniform)
                {
                    IntensityPanel.Visibility = Visibility.Visible;
                    DensityPanel.Visibility = Visibility.Collapsed;
                    NoiseColorPanel.Visibility = Visibility.Collapsed;
                }
                else // SaltAndPepper
                {
                    IntensityPanel.Visibility = Visibility.Collapsed;
                    DensityPanel.Visibility = Visibility.Visible;
                    NoiseColorPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void UpdateSizeModeVisibility()
        {
            if (cmbSizeMode?.SelectedValue is SolidSizeMode solidSizeMode)
            {
                if (solidSizeMode == SolidSizeMode.ByPixelSize)
                {
                    PixelStack.Visibility = Visibility.Visible;
                    FieldOfViewStack.Visibility = Visibility.Collapsed;
                }
                else // ByFieldOfView
                {
                    PixelStack.Visibility = Visibility.Collapsed;
                    FieldOfViewStack.Visibility = Visibility.Visible;
                }
            }
        }
    }
}

using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Checkerboard
{
    /// <summary>
    /// CheckerboardEditor.xaml 的交互逻辑
    /// </summary>
    public partial class CheckerboardEditor : UserControl
    {
        public static PatternCheckerboardConfig Config { get; set; }

        public CheckerboardEditor(PatternCheckerboardConfig patternCheckerboardConfig)
        {
            Config = patternCheckerboardConfig;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config; 
            cmbSizeMode.ItemsSource = from e1 in Enum.GetValues(typeof(CheckerboardSizeMode)).Cast<CheckerboardSizeMode>()
                                      select new KeyValuePair<CheckerboardSizeMode, string>(e1, e1.ToString());
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));

        }
        private void BtnPickMainColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectMainColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                Config.MainBrush = ColorPicker1.SelectedBrush;

            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.MainBrushTag = ColorPicker1.SelectedBrush.ToString();

                Config.MainBrush = ColorPicker1.SelectedBrush;

                window.Close();
            };
            window.Closed += (s, e) =>
            {
                ColorPicker1.Dispose();
            };
            window.Show();
        }

        private void BtnPickMainColorSet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag.ToString();
                Config.MainBrushTag = tag;
                if (tag == "R")
                {
                    Config.MainBrush = Brushes.Red;
                }
                if (tag == "G")
                {
                    Config.MainBrush = Brushes.Lime;
                }
                if (tag == "B")
                {
                    Config.MainBrush = Brushes.Blue;
                }
                if (tag == "W")
                {
                    Config.MainBrush = Brushes.White;
                }
                if (tag == "K")
                {
                    Config.MainBrush = Brushes.Black;
                }

            }
        }

        private void BtnPickAltColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectMainColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                Config.AltBrush = ColorPicker1.SelectedBrush;

            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.AltBrushTag = ColorPicker1.SelectedBrush.ToString();

                Config.AltBrush = ColorPicker1.SelectedBrush;

                window.Close();
            };
            window.Closed += (s, e) =>
            {
                ColorPicker1.Dispose();
            };
            window.Show();
        }

        private void BtnPickAltColorSet_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                string tag = button.Tag.ToString();
                Config.AltBrushTag = tag;
                if (tag == "R")
                {
                    Config.AltBrush = Brushes.Red;
                }
                if (tag == "G")
                {
                    Config.AltBrush = Brushes.Lime;
                }
                if (tag == "B")
                {
                    Config.AltBrush = Brushes.Blue;
                }
                if (tag == "W")
                {
                    Config.AltBrush = Brushes.White;
                }
                if (tag == "K")
                {
                    Config.AltBrush = Brushes.Black;
                }

            }
        }


        private void cmbSizeMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue is CheckerboardSizeMode checkerboardSizeMode)
            {
                if (checkerboardSizeMode == CheckerboardSizeMode.ByCellSize)
                {
                    CellStack.Visibility = Visibility.Visible;
                    GridStack.Visibility = Visibility.Collapsed;
                }
                if (checkerboardSizeMode == CheckerboardSizeMode.ByGridCount)
                {
                    CellStack.Visibility = Visibility.Collapsed;
                    GridStack.Visibility = Visibility.Visible;
                }
            }
        }
    }
}

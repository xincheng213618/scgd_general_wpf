using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.LinePairMTF
{
    /// <summary>
    /// LinePairMTFEditor.xaml 的交互逻辑
    /// </summary>
    public partial class LinePairMTFEditor : UserControl
    {
        public  PatternLinePairMTFConfig Config { get; set; }

        public LinePairMTFEditor(PatternLinePairMTFConfig config)
        {
            Config = config;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            StackPanelInfo.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(Config));
        }
        private void BtnPickMainColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectMainColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                Config.LineBrush = ColorPicker1.SelectedBrush;

            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.LineBrushTag = ColorPicker1.SelectedBrush.ToString();

                Config.LineBrush = ColorPicker1.SelectedBrush;

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
                Config.LineBrushTag = tag;
                if (tag == "R")
                {
                    Config.LineBrush = Brushes.Red;
                }
                if (tag == "G")
                {
                    Config.LineBrush = Brushes.Lime;
                }
                if (tag == "B")
                {
                    Config.LineBrush = Brushes.Blue;
                }
                if (tag == "W")
                {
                    Config.LineBrush = Brushes.White;
                }
                if (tag == "K")
                {
                    Config.LineBrush = Brushes.Black;
                }

            }
        }

        private void BtnPickAltColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectMainColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                Config.BackgroundBrush = ColorPicker1.SelectedBrush;
            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.BackgroundBrushTag = ColorPicker1.SelectedBrush.ToString();
                Config.BackgroundBrush = ColorPicker1.SelectedBrush;
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
                Config.BackgroundBrushTag = tag;
                if (tag == "R")
                {
                    Config.BackgroundBrush = Brushes.Red;
                }
                if (tag == "G")
                {
                    Config.BackgroundBrush = Brushes.Lime;
                }
                if (tag == "B")
                {
                    Config.BackgroundBrush = Brushes.Blue;
                }
                if (tag == "W")
                {
                    Config.BackgroundBrush = Brushes.White;
                }
                if (tag == "K")
                {
                    Config.BackgroundBrush = Brushes.Black;
                }
            }
        }
    }
}

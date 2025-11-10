using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pattern.Solid
{
    /// <summary>
    /// SolidEditor.xaml 的交互逻辑
    /// </summary>
    public partial class SolidEditor : UserControl
    {
        public PatternSolodConfig Config { get; set; }

        public SolidEditor(PatternSolodConfig patternSolodConfig)
        {
            Config = patternSolodConfig;
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
                Config.MainBrush = ColorPicker1.SelectedBrush;
            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.Tag = ColorPicker1.SelectedBrush.ToString();
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
                Config.Tag = tag;
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
    }
}

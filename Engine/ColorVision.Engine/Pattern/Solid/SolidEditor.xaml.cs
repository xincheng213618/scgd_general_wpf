using ColorVision.UI;
using System;
using System.Collections.Generic;
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

namespace ColorVision.Engine.Pattern.Solid
{
    /// <summary>
    /// DotEditor.xaml 的交互逻辑
    /// </summary>
    public partial class SolidEditor : UserControl
    {
        public SolidEditor()
        {
            InitializeComponent();
        }
        public static PatternSolodConfig Config => ConfigService.Instance.GetRequiredService<PatternSolodConfig>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
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
                if (tag == "R")
                {
                    Config.MainBrush = Brushes.Red;
                }
                if (tag == "G")
                {
                    Config.MainBrush = Brushes.Green;
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

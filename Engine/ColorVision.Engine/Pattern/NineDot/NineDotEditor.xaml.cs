using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern.NineDot
{
    /// <summary>
    /// NineDotEditor.xaml 的交互逻辑
    /// </summary>
    public partial class NineDotEditor : UserControl
    {
        public PatternNineDotConfig Config { get; set; }

        public NineDotEditor(PatternNineDotConfig patternNineDotConfig)
        {
            Config = patternNineDotConfig;
            InitializeComponent();
        }

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
                rectMainColor.Fill = Config.MainBrush;
            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.MainBrush = ColorPicker1.SelectedBrush;
                rectMainColor.Fill = Config.MainBrush;
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
                rectMainColor.Fill = Config.MainBrush;
            }
        }

        private void BtnPickAltColor_Click(object sender, RoutedEventArgs e)
        {
            var ColorPicker1 = new HandyControl.Controls.ColorPicker();
            ColorPicker1.SelectedBrush = (SolidColorBrush)rectMainColor.Fill;
            ColorPicker1.SelectedColorChanged += (s, e) =>
            {
                Config.AltBrush = ColorPicker1.SelectedBrush;
                rectAltColor.Fill = Config.AltBrush;
            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
                Config.AltBrush = ColorPicker1.SelectedBrush;
                rectAltColor.Fill = Config.AltBrush;
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
                rectAltColor.Fill = Config.AltBrush;
            }
        }
    }
}

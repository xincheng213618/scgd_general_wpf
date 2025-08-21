using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Pattern.CrossGrid
{
    /// <summary>
    /// CrossGridEditor.xaml 的交互逻辑
    /// </summary>
    public partial class CrossGridEditor : UserControl
    {
        public static PatternCrossGridConfig Config { get; set; }
        public CrossGridEditor(PatternCrossGridConfig patternCrossGridConfig)
        {
            Config = patternCrossGridConfig;
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
                Config.LineBrush = ColorPicker1.SelectedBrush;
            };
            Window window = new Window() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = ColorPicker1, Width = 250, Height = 400 };
            ColorPicker1.Confirmed += (s, e) =>
            {
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

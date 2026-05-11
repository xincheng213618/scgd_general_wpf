using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPhyCamera : UserControl
    {
        public PhyCamera Device { get; set; }
        public InfoPhyCamera(PhyCamera deviceCamera)
        {
            Device = deviceCamera;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
            ApplyGeneratedCommandButtonChrome();
        }

        private void ApplyGeneratedCommandButtonChrome()
        {
            Brush actionBackground = GetBrush("ButtonBackground", Brushes.Transparent);
            Brush actionBorder = GetBrush("GlobalBorderBrush1", Brushes.Gray);
            Brush actionText = GetBrush("GlobalTextBrush", Brushes.Black);
            Style? commandStyle = TryFindResource("OutlinedCommandButtonStyle") as Style;

            foreach (var child in CommandGrid.Children)
            {
                if (child is not Button commandButton)
                    continue;

                bool isDanger = commandButton.Foreground is SolidColorBrush brush && brush.Color == Colors.Red;

                if (commandStyle != null)
                    commandButton.Style = commandStyle;

                commandButton.Height = 56;
                commandButton.MinHeight = 56;
                commandButton.Margin = new Thickness(4);
                commandButton.Padding = new Thickness(12, 6, 12, 6);
                commandButton.Background = actionBackground;
                commandButton.BorderBrush = isDanger ? Brushes.Red : actionBorder;
                commandButton.BorderThickness = new Thickness(1);
                commandButton.Foreground = isDanger ? Brushes.Red : actionText;
                commandButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                commandButton.VerticalContentAlignment = VerticalAlignment.Center;

                foreach (var textBlock in FindVisualChildren<TextBlock>(commandButton))
                {
                    textBlock.Foreground = isDanger ? Brushes.Red : actionText;
                    textBlock.TextAlignment = TextAlignment.Center;
                    textBlock.FontSize = textBlock.FontSize <= 12 ? 12 : textBlock.FontSize;
                    textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
                }
            }
        }

        private static Brush GetBrush(string key, Brush fallback)
        {
            return Application.Current.TryFindResource(key) as Brush ?? fallback;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    yield return typedChild;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
    }
}

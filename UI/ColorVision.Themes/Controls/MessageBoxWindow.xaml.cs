using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ColorVision.Themes.Controls
{
    /// <summary>A themed, selectable message with standard message-box results.</summary>
    public partial class MessageBoxWindow : Window, INotifyPropertyChanged
    {
        private readonly MessageBoxButton buttons;
        private readonly Button defaultButton;
        private bool dontShowAgain;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public bool DontShowAgain
        {
            get => dontShowAgain;
            set
            {
                if (dontShowAgain == value) return;
                dontShowAgain = value;
                NotifyPropertyChanged();
            }
        }

        public MessageBoxResult MessageBoxResult { get; set; } = MessageBoxResult.None;

        public MessageBoxWindow(string messageBoxText) : this(messageBoxText, Properties.Resources.MsgBox_Prompt) { }
        public MessageBoxWindow(string messageBoxText, string caption) : this(messageBoxText, caption, MessageBoxButton.OK) { }
        public MessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button) : this(messageBoxText, caption, button, MessageBoxImage.None) { }
        public MessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon) : this(messageBoxText, caption, button, icon, MessageBoxResult.None) { }

        public MessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        {
            if (!Enum.IsDefined(button)) throw new InvalidEnumArgumentException(nameof(button), (int)button, typeof(MessageBoxButton));
            if (!Enum.IsDefined(icon)) throw new InvalidEnumArgumentException(nameof(icon), (int)icon, typeof(MessageBoxImage));
            if (!Enum.IsDefined(defaultResult)) throw new InvalidEnumArgumentException(nameof(defaultResult), (int)defaultResult, typeof(MessageBoxResult));

            InitializeComponent();
            this.ApplyCaption();
            buttons = button;
            this.messageBoxText.Text = messageBoxText ?? string.Empty;
            Title = caption ?? Properties.Resources.MsgBox_Prompt;
            AutomationProperties.SetName(this.messageBoxText, this.messageBoxText.Text);
            DataContext = this;

            ConfigureButton(ButtonOK, MessageBoxResult.OK, button is MessageBoxButton.OK or MessageBoxButton.OKCancel);
            ConfigureButton(ButtonYes, MessageBoxResult.Yes, button is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel);
            ConfigureButton(ButtonNo, MessageBoxResult.No, button is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel);
            ConfigureButton(ButtonCancel, MessageBoxResult.Cancel, button is MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel);
            ButtonCancel.IsCancel = ButtonCancel.Visibility == Visibility.Visible;

            Button candidate = defaultResult switch
            {
                MessageBoxResult.Cancel => ButtonCancel,
                MessageBoxResult.No => ButtonNo,
                MessageBoxResult.Yes => ButtonYes,
                _ => ButtonOK
            };
            defaultButton = candidate.Visibility == Visibility.Visible ? candidate : ButtonYes;
            if (defaultButton.Visibility != Visibility.Visible) defaultButton = ButtonOK;
            defaultButton.IsDefault = true;
            ConfigureIcon(icon);
        }

        private static void ConfigureButton(Button button, MessageBoxResult result, bool visible)
        {
            button.Tag = result;
            button.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ConfigureIcon(MessageBoxImage icon)
        {
            if (icon == MessageBoxImage.None) return;
            IconHost.Visibility = Visibility.Visible;
            string circle = "M 12,2 A 10,10 0 1 1 12,22 A 10,10 0 1 1 12,2 ";
            string geometry = icon switch
            {
                MessageBoxImage.Error => circle + "M 8,8 L 16,16 M 16,8 L 8,16",
                MessageBoxImage.Warning => "M 12,3 L 22,21 L 2,21 Z M 12,9 L 12,14 M 12,17 L 12,17.2",
                MessageBoxImage.Question => circle + "M 9,8 A 3,3 0 1 1 14,11 C 12,12 12,12 12,14 M 12,17 L 12,17.2",
                _ => circle + "M 12,10 L 12,17 M 12,6.8 L 12,7"
            };
            IconPath.Data = Geometry.Parse(geometry);
            if (icon == MessageBoxImage.Warning) IconPath.Stroke = new SolidColorBrush(Color.FromRgb(190, 124, 20));
            if (icon == MessageBoxImage.Error) IconPath.Stroke = new SolidColorBrush(Color.FromRgb(217, 78, 93));
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // The HWND is on the owner's monitor now; convert that monitor's working area to WPF DIPs.
            var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
            var dpi = VisualTreeHelper.GetDpi(this);
            double availableWidth = area.Width / dpi.DpiScaleX;
            double availableHeight = area.Height / dpi.DpiScaleY;
            double iconWidth = IconHost.Visibility == Visibility.Visible ? 56 : 0;
            // Measure the actual TextBox, including its selection gutter and font fallback.
            messageBoxText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            MaxWidth = Math.Min(640, availableWidth * 0.9);
            Width = Math.Min(MaxWidth, Math.Max(360, messageBoxText.DesiredSize.Width + iconWidth + 64));
            // Reserve space for caption, content padding, buttons and the optional checkbox.
            messageBoxText.MaxHeight = Math.Max(48, Math.Min(480, availableHeight * 0.8 - 190 - (show_again.Visibility == Visibility.Visible ? 40 : 0)));
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            defaultButton.Focus();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            base.OnPreviewKeyDown(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // A default button is a keyboard choice, never an implicit confirmation on dismissal.
            if (MessageBoxResult == MessageBoxResult.None)
                MessageBoxResult = buttons switch
                {
                    MessageBoxButton.OK => MessageBoxResult.OK,
                    MessageBoxButton.YesNo => MessageBoxResult.No,
                    _ => MessageBoxResult.Cancel
                };
            base.OnClosing(e);
            if (e.Cancel) MessageBoxResult = MessageBoxResult.None;
        }

        private void ResultButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult = (MessageBoxResult)((Button)sender).Tag;
            Close();
        }
    }
}

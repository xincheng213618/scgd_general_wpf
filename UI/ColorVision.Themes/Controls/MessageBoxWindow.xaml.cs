using ColorVision.Common.Utilities;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ColorVision.Themes.Controls
{
    /// <summary>
    /// MessageBoxWindow.xaml 的交互逻辑
    /// </summary>


    public partial class MessageBoxWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// 不在提示
        /// </summary>
        public bool DontShowAgain { get => _DontShowAgain; set { _DontShowAgain = value; NotifyPropertyChanged(); } }
        private bool _DontShowAgain;

        /// <summary>
        /// 显示结果
        /// </summary>
        public MessageBoxResult MessageBoxResult { get; set; } = MessageBoxResult.None;
        private void Init(string messageBoxText, string caption = "提示", MessageBoxButton button = MessageBoxButton.OK , MessageBoxImage icon = MessageBoxImage.None ,MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            InitializeComponent();
            this.ApplyCaption();
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            DockMsg.MaxHeight = screenHeight / 2;
            this.messageBoxText.Text = messageBoxText;
            this.Title = caption;
            switch (button)
            {
                case MessageBoxButton.OK:
                    ButtonOK.Visibility = Visibility.Visible;
                    ButtonOK.Focus();
                    break;
                case MessageBoxButton.OKCancel:
                    ButtonOK.Visibility = Visibility.Visible;
                    ButtonOK.Focus();
                    ButtonCancel.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    ButtonCancel.Visibility = Visibility.Visible;
                    ButtonYes.Focus();
                    break;
                case MessageBoxButton.YesNo:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    ButtonYes.Focus();
                    break;
                default:
                    break;
            }
            //MessageBoxImage 中存在重复项和重复样式，所以这几项就可以了
            switch (icon)
            {
                case MessageBoxImage.None:
                    Imageicon.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxImage.Error:
                    Imageicon.Source = SystemIcons.Error.ToImageSource();
                    break;
                case MessageBoxImage.Question:
                    Imageicon.Source = SystemIcons.Question.ToImageSource();
                    break;
                case MessageBoxImage.Warning:
                    Imageicon.Source = SystemIcons.Warning.ToImageSource();
                    break;
                case MessageBoxImage.Information:
                    Imageicon.Source = SystemIcons.Information.ToImageSource();
                    break;
                default:
                    break;
            }
            MessageBoxResult = defaultResult;
            this.DataContext = this;
        }


        public MessageBoxWindow(string messageBoxText)
        {
            Init(messageBoxText);
        }
        public MessageBoxWindow(string messageBoxText,string caption)
        {
            Init(messageBoxText, caption);
        }
        public MessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button)
        {
            Init(messageBoxText, caption, button);
        }

        public MessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button,MessageBoxImage icon)
        {
            Init(messageBoxText, caption, button, icon);
        }
        public MessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon,MessageBoxResult defaultResult)
        {
            Init(messageBoxText, caption, button, icon, defaultResult);
        }
        



        private void Yes_Button_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult = MessageBoxResult.Yes;
            Close();
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult = MessageBoxResult.Cancel;
            Close();
        }

        private void ButtonNo_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult = MessageBoxResult.No;
            Close();
        }

        private void ButtonYes_Click(object sender, RoutedEventArgs e)
        {
            Close();
            MessageBoxResult = MessageBoxResult.Yes;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult = MessageBoxResult.OK;
            Close();
        }


    }
}

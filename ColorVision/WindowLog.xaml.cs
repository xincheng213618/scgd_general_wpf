using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision
{
    public class WindowLogExport : IHotKey, IMenuItem
    {
        public HotKeys HotKeys => new(Properties.Resources.Log, new Hotkey(Key.F2, ModifierKeys.Control), Execute);

        public string? OwnerGuid => "Help";

        public string? GuidId => "WindowLog";

        public int Order => 10005;

        public string? Header => ColorVision.Properties.Resources.Log;

        public string? InputGestureText => "Ctrl + F2";

        public object? Icon => null;

        public RelayCommand Command => new(A => Execute());

        private void Execute()
        {
            new WindowLog() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
        public Visibility Visibility => Visibility.Visible;
    }


    /// <summary>
    /// WindowLog.xaml 的交互逻辑
    /// </summary>
    public partial class WindowLog : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowLog));

        public WindowLog()
        {
            InitializeComponent();


            var hierarchy = (Hierarchy)LogManager.GetRepository();
            //hierarchy.Root.RemoveAllAppenders();
            // 创建一个输出到TextBox的Appender
            var textBoxAppender = new TextBoxAppender(logTextBox);

            // 设置布局格式
            var layout = new PatternLayout("%date [%thread] %-5level %logger - %message%newline");
            textBoxAppender.Layout = layout;

            // 将Appender添加到Logger中
            hierarchy.Root.AddAppender(textBoxAppender);

            // 配置并激活log4net
            log4net.Config.BasicConfigurator.Configure(hierarchy);

        }
    }
    public class TextBoxAppender : AppenderSkeleton
    {
        public TextBoxAppender(TextBox textBox)
        {
            _textBox = textBox;
        }

        private TextBox _textBox;
        protected override void Append(LoggingEvent loggingEvent)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _textBox.AppendText(loggingEvent.RenderedMessage + Environment.NewLine);
            });
        }
    }
}

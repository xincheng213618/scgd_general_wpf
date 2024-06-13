using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            //hierarchy.Root.RemoveAllAppenders();
            // 创建一个输出到TextBox的Appender
            var textBoxAppender = new TextBoxAppender(logTextBox);

            // 设置布局格式
            var layout = new PatternLayout("%date [%thread] %-5level %logger %newline %message%newline");
            textBoxAppender.Layout = layout;
            // 将Appender添加到Logger中
            hierarchy.Root.AddAppender(textBoxAppender);

            LoadLogHistory();
            // 配置并激活log4net
            log4net.Config.BasicConfigurator.Configure(hierarchy);
            cmlog.DataContext = MainWindowConfig.Instance;
            cmlog.ItemsSource = MainWindowConfig.GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));
        }
        private static string GetLogFilePath()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var fileAppender = hierarchy.Root.Appenders.OfType<RollingFileAppender>().FirstOrDefault();
            return fileAppender?.File;
        }
        private void LoadLogHistory()
        {
            var logFilePath = GetLogFilePath();
            if (logFilePath != null && File.Exists(logFilePath))
            {
                try
                {
                    using (FileStream fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(fileStream, Encoding.Default))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            logTextBox.AppendText(line + Environment.NewLine);
                        }
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Error reading log file: {ex.Message}");
                }
            }
        }
        private void cmlog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedLevel = (KeyValuePair<Level, string>)cmlog.SelectedItem;
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            if (selectedLevel.Key != hierarchy.Root.Level)
            {
                hierarchy.Root.Level = selectedLevel.Key;
                log4net.Config.BasicConfigurator.Configure(hierarchy);
                log.Info("更新Log4Net 日志级别：" + selectedLevel.Value);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            logTextBox.Text = string.Empty;
        }

        private void Loadhistory_Click(object sender, RoutedEventArgs e)
        {
            logTextBox.Text = string.Empty;
            LoadLogHistory();
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
            if (MainWindowConfig.Instance.AutoRefresh) return;
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            Application.Current.Dispatcher.Invoke(() =>
            {
                _textBox.AppendText(renderedMessage);
                if (MainWindowConfig.Instance.AutoScrollToEnd)
                    _textBox.ScrollToEnd();
            });
        }
    }
}

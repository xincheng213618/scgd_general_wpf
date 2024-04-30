using log4net.Layout;
using log4net.Repository.Hierarchy;
using log4net;
using System;
using System.Windows;
using System.Windows.Controls;
using log4net.Appender;
using log4net.Core;
using ColorVision.MQTT;

namespace ColorVision
{


    /// <summary>
    /// WindowLog.xaml 的交互逻辑
    /// </summary>
    public partial class WindowLog : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowLog));

        public WindowLog()
        {
            InitializeComponent();

            MQTTControl.GetInstance().MQTTMsgChanged += (e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    log.Info(e.ResultMsg);
                });
            };

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

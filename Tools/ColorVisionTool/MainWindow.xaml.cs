using ColorVision.Common.MVVM;
using CsharpDEMO;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVisionTool
{


    public class MotorInfo:ViewModelBase
    {
        public int Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private int _Position;

        public double Accuracy { get => _Accuracy; set { _Accuracy = value; NotifyPropertyChanged(); } }
        private double _Accuracy;
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
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            Application.Current.Dispatcher.Invoke(() =>
            {
                _textBox.AppendText(renderedMessage);
                _textBox.ScrollToEnd();
            });
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));

        public MainWindow()
        {
            InitializeComponent();
        }

        DemoType demoType = new DemoType();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            log.Info("正在进行自动聚焦测试");


            Task.Run(() =>
            {
                demoType.testMotor();
            });
        }

        public MotorInfo MotorInfo { get; set; } = new MotorInfo();

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {

        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            //hierarchy.Root.RemoveAllAppenders();
            // 创建一个输出到TextBox的Appender
            var textBoxAppender = new TextBoxAppender(TexBoxLog);

            // 设置布局格式
            var layout = new PatternLayout("%date% %message%newline");
            textBoxAppender.Layout = layout;
            // 将Appender添加到Logger中
            hierarchy.Root.AddAppender(textBoxAppender);

            // 配置并激活log4net
            log4net.Config.BasicConfigurator.Configure(hierarchy);

            log.Info("软件启动");

            demoType.Image1 = Image1.ImageShow;
            demoType.Zoombox1 = Image1.Zoombox1;
            demoType.MotorInfo = MotorInfo;

            StackPanelInfo.DataContext = MotorInfo;
        }

        private void MoveTo_click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TextBoxPostion.Text,out int pos))
            {
                demoType.MoveTo(pos);
            }

        }

        private void TakePhoto_Click(object sender, RoutedEventArgs e)
        {
            demoType.TakePhoto();

        }

        private void OpenOutputFile_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Utilities.PlatformHelper.OpenFolder(AppDomain.CurrentDomain.BaseDirectory);
        }
    }
    
}
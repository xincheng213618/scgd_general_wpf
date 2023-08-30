using ColorVision.MQTT.Service;
using ColorVision.MQTT;
using ColorVision.Template;
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
using ColorVision.SettingUp;
using System.IO;

namespace ColorVision.Flow
{
    /// <summary>
    /// FlowDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class FlowDisplayControl : UserControl
    {
        public FlowView flowView { get; set; }

        public FlowDisplayControl()
        {
            InitializeComponent();
        }

        public GlobalSetting GlobalSetting { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            GlobalSetting = GlobalSetting.GetInstance();
            MQTTConfig mQTTConfig = GlobalSetting.SoftwareConfig.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowView = new FlowView();
            ViewGridManager.GetInstance().AddView(flowView);


            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>("独立窗口", -2));
                KeyValues.Add(new KeyValuePair<string, int>("隐藏", -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = flowView.View.ViewIndex;
            };
            flowView.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    flowView.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(flowView, KeyValue.Value);
                }
            };


        FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;
            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedValue is FlowParam flowParam)
                {
                    string fileName = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.GetFullFileName(flowParam.FileName ?? string.Empty);
                    if (File.Exists(fileName))
                    {
                        if (flowView != null)
                        {
                            try
                            {
                                flowView.FlowEngineControl.Load(fileName);
                            }
                            catch
                            {

                            }
                        }
                    }
                }
            };
            FlowTemplate.SelectedIndex = 0;

        }


        private FlowControl flowControl;
        Window window;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            //MessageBox.Show("流程执行完成");
            window.Close();

            if (sender != null)
            {
                FlowControlData flowControlData = (FlowControlData)sender;
                ServiceControl.GetInstance().SpectrumDrawPlotFromDB(flowControlData.SerialNumber);
            }
        }

        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = flowView.FlowEngineControl.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl = new FlowControl(MQTTControl.GetInstance(), flowView.FlowEngineControl);

                    window = new Window() { Width = 400, Height = 400, Title = "流程返回信息", Owner = Application.Current.MainWindow, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    TextBox textBox = new TextBox() { IsReadOnly = true, Background = Brushes.Black, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

                    Grid grid = new Grid();
                    grid.Children.Add(textBox);

                    grid.Children.Add(new Controls.ProgressRing() { Margin = new Thickness(100, 100, 100, 100) });

                    window.Content = grid;

                    textBox.Text = "TTL:" + "0";
                    flowControl.FlowData += (s, e) =>
                    {
                        if (s is FlowControlData msg)
                        {
                            textBox.Text = "TTL:" + msg.Params.TTL.ToString();
                        }
                    };
                    flowControl.FlowCompleted += FlowControl_FlowCompleted;
                    string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    ServiceControl.GetInstance().ResultBatchSave(sn);
                    flowControl.Start(sn);
                    window.Show();
                }
                else
                {
                    MessageBox.Show("流程模板为空，不能运行！！！");
                }
            }
        }

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
        {
            if (flowControl != null)
            {
                flowControl.Stop();
            }
        }

    }
}

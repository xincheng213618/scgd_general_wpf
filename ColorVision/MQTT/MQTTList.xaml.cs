using HslCommunication;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.MQTT
{
    /// <summary>
    /// MQTTList.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTList : Window
    {

        public ObservableCollection<MsgRecord> MsgRecords { get; set; }
        public MQTTControl MQTTControl { get; set; }


        public MQTTList()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTControl = MQTTControl.GetInstance();
            MsgRecords = MQTTControl.MQTTSetting.MsgRecords;
            ListView1.ItemsSource = MsgRecords;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void StackPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is StackPanel stackPanel  )
            {
                if (stackPanel.Tag is MsgReturn msgReturn)
                {
                    MessageBox.Show(JsonSerializer.Serialize(msgReturn, new JsonSerializerOptions() { WriteIndented = true }));
                }
                else if (stackPanel.Tag is MsgSend msgSend)
                {
                    MessageBox.Show(JsonSerializer.Serialize(msgSend, new JsonSerializerOptions() { WriteIndented = true }));

                }
            }
        }
    }
}

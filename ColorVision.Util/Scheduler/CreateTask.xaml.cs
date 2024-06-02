using ColorVision.Common.Utilities;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Scheduler
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTask : Window
    {
        public SchedulerInfo SchedulerInfo { get; set; }
        public CreateTask()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            TaskComboBox.ItemsSource = QuartzSchedulerManager.GetInstance().Jobs;
            SchedulerInfo = new SchedulerInfo();
            this.DataContext = SchedulerInfo;
        }


        private void TaskComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SchedulerInfo.JobName = QuartzSchedulerManager.GetInstance().GetNewJobName(SchedulerInfo.JobType.Name);
            SchedulerInfo.GroupName = QuartzSchedulerManager.GetInstance().GetNewGroupName(SchedulerInfo.JobType.Name);
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var jobName = SchedulerInfo.JobName;
            var groupName = SchedulerInfo.GroupName;
            var cronExpression = SchedulerInfo.CronExpression;

            if (string.IsNullOrWhiteSpace(jobName) || string.IsNullOrWhiteSpace(groupName) || string.IsNullOrWhiteSpace(cronExpression))
            {
                MessageBox.Show("Please fill in all fields.");
                return;
            }
            var taskInfo = QuartzSchedulerManager.GetInstance().CreateJob(SchedulerInfo);
            this.Close();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }


    }
}

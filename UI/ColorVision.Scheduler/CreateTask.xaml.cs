using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Scheduler
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTask : Window
    {
        public SchedulerInfo SchedulerInfo { get; set; } = new SchedulerInfo();
        public CreateTask()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ComboBoxMode.ItemsSource = from e1 in Enum.GetValues(typeof(JobExecutionMode)).Cast<JobExecutionMode>()
                                       select new KeyValuePair<JobExecutionMode, string>(e1, e1.ToString());

            TaskComboBox.ItemsSource = QuartzSchedulerManager.GetInstance().Jobs;
            this.DataContext = SchedulerInfo;
        }


        private void TaskComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SchedulerInfo.JobType != null)
            {
                SchedulerInfo.JobName = QuartzSchedulerManager.GetInstance().GetNewJobName(SchedulerInfo.JobType.Name);
                SchedulerInfo.GroupName = QuartzSchedulerManager.GetInstance().GetNewGroupName(SchedulerInfo.JobType.Name);

                StackPanelConfig.Children.Clear();
                
                // Check if job supports configuration
                if (typeof(IConfigurableJob).IsAssignableFrom(SchedulerInfo.JobType))
                {
                    try
                    {
                        // Create an instance to get config type
                        var jobInstance = Activator.CreateInstance(SchedulerInfo.JobType) as IConfigurableJob;
                        if (jobInstance != null)
                        {
                            // Create or reuse config
                            if (SchedulerInfo.Config == null || SchedulerInfo.Config.GetType() != jobInstance.ConfigType)
                            {
                                SchedulerInfo.Config = jobInstance.CreateDefaultConfig();
                            }
                            
                            // Generate UI for config
                            if (SchedulerInfo.Config != null)
                            {
                                var configPanel = PropertyEditorHelper.GenPropertyEditorControl(SchedulerInfo.Config);
                                if (configPanel != null)
                                {
                                    StackPanelConfig.Children.Add(configPanel);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to create job configuration: {ex.Message}");
                    }
                }
            }

        }
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var jobName = SchedulerInfo.JobName;
            var groupName = SchedulerInfo.GroupName;
            var cronExpression = SchedulerInfo.CronExpression;

            if (string.IsNullOrWhiteSpace(jobName) || string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.Show("Please fill in all fields.");
                return;
            }
            // 判断是新增还是编辑
            var isEdit = QuartzSchedulerManager.GetInstance().TaskInfos.Any(x => x.JobName == jobName && x.GroupName == groupName);
            if (isEdit)
            {
                await QuartzSchedulerManager.GetInstance().UpdateJob(SchedulerInfo);
            }
            else
            {
                await QuartzSchedulerManager.GetInstance().CreateJob(SchedulerInfo);
            }
            this.DialogResult = true;
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


        private void ComboBoxRepeat_Initialized(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = from e1 in Enum.GetValues(typeof(JobRepeatMode)).Cast<JobRepeatMode>()
                                       select new KeyValuePair<string,JobRepeatMode>(e1.ToDescription(), e1);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            PlatformHelper.Open("https://cron.qqe2.com/");
        }
    }
}

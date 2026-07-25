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
        private SchedulerInfo _schedulerInfo = new();
        private string? _originalJobName;
        private string? _originalGroupName;
        private bool _suppressSelectionChanged;

        public SchedulerInfo SchedulerInfo
        {
            get => _schedulerInfo;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _schedulerInfo = value;

                if (_originalJobName == null
                    && QuartzSchedulerManager.GetInstance().TaskInfos.Any(info =>
                        info.JobName == value.JobName && info.GroupName == value.GroupName))
                {
                    _originalJobName = value.JobName;
                    _originalGroupName = value.GroupName;
                }

                if (IsInitialized)
                    BindSchedulerInfo();
            }
        }

        private bool IsEditing => _originalJobName != null && _originalGroupName != null;

        public CreateTask()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _suppressSelectionChanged = true;
            ComboBoxMode.ItemsSource = from mode in Enum.GetValues<JobExecutionMode>()
                                       select new KeyValuePair<string, JobExecutionMode>(GetExecutionModeDisplay(mode), mode);
            TaskComboBox.ItemsSource = QuartzSchedulerManager.GetInstance().Jobs;
            DataContext = SchedulerInfo;
            _suppressSelectionChanged = false;
            RenderConfigurationEditor();
        }

        private static string GetExecutionModeDisplay(JobExecutionMode mode)
        {
            return mode == JobExecutionMode.Calendar
                ? $"{mode.ToDescription()} ({Properties.Resources.Sched_Interval}: 24 h)"
                : mode.ToDescription();
        }


        private void TaskComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged || !ReferenceEquals(sender, TaskComboBox) || SchedulerInfo.JobType == null)
                return;

            if (!IsEditing)
            {
                SchedulerInfo.JobName = QuartzSchedulerManager.GetInstance().GetNewJobName(SchedulerInfo.JobType.Name);
                SchedulerInfo.GroupName = QuartzSchedulerManager.GetInstance().GetNewGroupName(SchedulerInfo.JobType.Name);
            }

            RenderConfigurationEditor();
        }

        private void BindSchedulerInfo()
        {
            _suppressSelectionChanged = true;
            DataContext = SchedulerInfo;
            _suppressSelectionChanged = false;
            RenderConfigurationEditor();
        }

        private void RenderConfigurationEditor()
        {
            StackPanelConfig.Children.Clear();
            if (SchedulerInfo.JobType == null || !typeof(IConfigurableJob).IsAssignableFrom(SchedulerInfo.JobType))
                return;

            try
            {
                if (Activator.CreateInstance(SchedulerInfo.JobType) is IConfigurableJob jobInstance)
                {
                    if (SchedulerInfo.Config == null || SchedulerInfo.Config.GetType() != jobInstance.ConfigType)
                        SchedulerInfo.Config = jobInstance.CreateDefaultConfig();

                    if (SchedulerInfo.Config != null)
                    {
                        var configPanel = PropertyEditorHelper.GenPropertyEditorControl(SchedulerInfo.Config);
                        if (configPanel != null)
                            StackPanelConfig.Children.Add(configPanel);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to create configuration for job type '{SchedulerInfo.JobType.Name}': {ex.Message}\n\nThe job will be created without custom configuration.",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                QuartzSchedulerManager manager = QuartzSchedulerManager.GetInstance();
                SchedulerOperationResult result = IsEditing
                    ? await manager.UpdateJob(SchedulerInfo, _originalJobName!, _originalGroupName!)
                    : await manager.CreateJob(SchedulerInfo);

                if (!result.Success)
                {
                    MessageBox.Show(
                        result.Message,
                        result.Error == SchedulerOperationError.Validation
                            ? Properties.Resources.Sched_ParamError
                            : Properties.Resources.Sched_Error,
                        MessageBoxButton.OK,
                        result.Error == SchedulerOperationError.Validation
                            ? MessageBoxImage.Warning
                            : MessageBoxImage.Error);
                    return;
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    Properties.Resources.Sched_Error,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
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
                comboBox.ItemsSource = from e1 in Enum.GetValues<JobRepeatMode>().Cast<JobRepeatMode>()
                                       select new KeyValuePair<string,JobRepeatMode>(e1.ToDescription(), e1);
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            PlatformHelper.Open("https://cron.qqe2.com/");
        }
    }
}

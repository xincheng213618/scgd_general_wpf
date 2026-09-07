using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Scheduler
{
    public partial class CreateTask : Window
    {
        private readonly ISchedulerService _schedulerService;
        private readonly Task _initializationTask;
        private SchedulerInfo _schedulerInfo = new();
        private string? _originalJobName;
        private string? _originalGroupName;
        private string? _suggestedJobName;
        private string? _suggestedGroupName;
        private string? _configurationError;
        private bool _suppressSelectionChanged;
        private bool _isSaving;
        private bool _loaded;
        private bool _isClosed;

        public SchedulerInfo SchedulerInfo
        {
            get => _schedulerInfo;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _schedulerInfo = value;
                if (_originalJobName == null && _schedulerService.TaskInfos.Any(info => info.JobName == value.JobName && info.GroupName == value.GroupName))
                {
                    _originalJobName = value.JobName;
                    _originalGroupName = value.GroupName;
                }
                if (IsInitialized)
                    BindSchedulerInfo();
            }
        }

        private bool IsEditing => _originalJobName != null && _originalGroupName != null;

        public CreateTask() : this(QuartzSchedulerManager.GetInstance(), QuartzSchedulerManager.GetInstance().InitializationTask) { }

        internal CreateTask(ISchedulerService schedulerService, Task? initializationTask = null)
        {
            _schedulerService = schedulerService;
            _initializationTask = initializationTask ?? Task.CompletedTask;
            InitializeComponent();
            MaxHeight = Math.Max(320, SystemParameters.WorkArea.Height - 48);
            SubmitButton.IsEnabled = false;
            BindSchedulerInfo();
            this.ApplyCaption();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                await _initializationTask;
                if (_isClosed)
                    return;
                _suppressSelectionChanged = true;
                DataContext = null;
                ComboBoxMode.ItemsSource = Enum.GetValues<JobExecutionMode>()
                    .Select(mode => new KeyValuePair<string, JobExecutionMode>(mode.ToDescription(), mode));
                RepeatModeComboBox.ItemsSource = Enum.GetValues<JobRepeatMode>()
                    .Select(mode => new KeyValuePair<string, JobRepeatMode>(mode.ToDescription(), mode));
                TaskComboBox.ItemsSource = _schedulerService.Jobs.OrderBy(entry => entry.Key).ToArray();
                BindSchedulerInfo();
                _suppressSelectionChanged = false;
                SubmitButton.IsEnabled = _schedulerService.Jobs.Count > 0;
                if (_schedulerService.Jobs.Count == 0)
                    ShowError(Properties.Resources.Sched_NoTaskTypes);
                else if (SchedulerInfo.JobType == null && _schedulerService.Jobs.Count == 1)
                    TaskComboBox.SelectedIndex = 0;
                TaskComboBox.Focus();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
        }

        private void BindSchedulerInfo()
        {
            _suppressSelectionChanged = true;
            DataContext = SchedulerInfo;
            Title = FormTitle.Text = IsEditing ? Properties.Resources.Sched_EditTask : Properties.Resources.CreateTask;
            FormSubtitle.Text = IsEditing ? Properties.Resources.Sched_EditHint : Properties.Resources.Sched_CreateHint;
            SubmitButton.Content = IsEditing ? Properties.Resources.Sched_SaveChanges : Properties.Resources.CreateTask;
            AdvancedSettings.IsExpanded = SchedulerInfo.Priority != 5 || SchedulerInfo.TimeoutSeconds != 0;
            _suppressSelectionChanged = false;
            RenderConfigurationEditor();
        }

        private void TaskComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged || TaskComboBox.SelectedValue is not Type type)
                return;
            SchedulerInfo.JobType = type;
            if (!IsEditing)
            {
                // Suggestions must not overwrite a name the user has already entered.
                if (string.IsNullOrWhiteSpace(SchedulerInfo.JobName) || SchedulerInfo.JobName == _suggestedJobName)
                    SchedulerInfo.JobName = _suggestedJobName = _schedulerService.GetNewJobName(type.Name);
                if (string.IsNullOrWhiteSpace(SchedulerInfo.GroupName) || SchedulerInfo.GroupName == _suggestedGroupName)
                    SchedulerInfo.GroupName = _suggestedGroupName = _schedulerService.GetNewGroupName(type.Name);
            }
            RenderConfigurationEditor();
        }

        private void RenderConfigurationEditor()
        {
            StackPanelConfig.Children.Clear();
            ConfigurationSection.Visibility = Visibility.Collapsed;
            if (ErrorMessage.Text == _configurationError)
                ErrorMessage.Visibility = Visibility.Collapsed;
            _configurationError = null;
            if (SchedulerInfo.JobType == null)
                return;
            if (!typeof(IConfigurableJob).IsAssignableFrom(SchedulerInfo.JobType))
            {
                SchedulerInfo.Config = null!;
                return;
            }
            try
            {
                if (Activator.CreateInstance(SchedulerInfo.JobType) is IConfigurableJob job)
                {
                    if (SchedulerInfo.Config == null || SchedulerInfo.Config.GetType() != job.ConfigType)
                        SchedulerInfo.Config = job.CreateDefaultConfig();
                    if (SchedulerInfo.Config != null && PropertyEditorHelper.GenPropertyEditorControl(SchedulerInfo.Config) is { } editor)
                    {
                        StackPanelConfig.Children.Add(editor);
                        ConfigurationSection.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                _configurationError = string.Format(Properties.Resources.Sched_ConfigFailed, ex.Message);
                ShowError(_configurationError);
            }
        }

        private void DelayStart_Checked(object sender, RoutedEventArgs e)
        {
            if (SchedulerInfo.Delay == TimeSpan.Zero)
                SchedulerInfo.Delay = TimeSpan.FromMinutes(5);
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (await SaveAsync())
                DialogResult = true;
        }

        internal async Task<bool> SaveAsync()
        {
            if (_isSaving || !SubmitButton.IsEnabled)
                return false;
            ErrorMessage.Visibility = Visibility.Collapsed;
            SubmitButton.Focus();
            if (FindInvalidInput(EditorForm) is UIElement invalidInput)
            {
                ShowError(Properties.Resources.Sched_InvalidInput);
                invalidInput.Focus();
                if (invalidInput is FrameworkElement element)
                    element.BringIntoView();
                return false;
            }

            string? error = _configurationError ?? SchedulerTriggerFactory.Validate(SchedulerInfo);
            if (error != null)
            {
                ShowError(error);
                return false;
            }

            _isSaving = true;
            EditorForm.IsEnabled = CancelButton.IsEnabled = SubmitButton.IsEnabled = false;
            SubmitButton.Content = Properties.Resources.Sched_Working;
            try
            {
                SchedulerOperationResult result = IsEditing
                    ? await _schedulerService.UpdateJob(SchedulerInfo, _originalJobName!, _originalGroupName!)
                    : await _schedulerService.CreateJob(SchedulerInfo);
                if (!result.Success)
                    ShowError(result.Message);
                return result.Success;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
                return false;
            }
            finally
            {
                _isSaving = false;
                EditorForm.IsEnabled = CancelButton.IsEnabled = SubmitButton.IsEnabled = true;
                SubmitButton.Content = IsEditing ? Properties.Resources.Sched_SaveChanges : Properties.Resources.CreateTask;
            }
        }

        private static UIElement? FindInvalidInput(DependencyObject root)
        {
            if (root is UIElement { Visibility: not Visibility.Visible })
                return null;
            if (root is TextBox textBox)
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (root is UIElement element && Validation.GetHasError(root))
                return element;
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                if (FindInvalidInput(VisualTreeHelper.GetChild(root, index)) is { } invalid)
                    return invalid;
            }
            return null;
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = Visibility.Visible;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isSaving)
                e.Cancel = true;
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            base.OnClosed(e);
        }

        private void CronGenerator_Click(object sender, RoutedEventArgs e) => PlatformHelper.Open("https://cron.qqe2.com/");
    }
}

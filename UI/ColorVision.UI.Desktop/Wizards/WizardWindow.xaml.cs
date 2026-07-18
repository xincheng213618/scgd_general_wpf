#pragma warning disable CA1805
using ColorVision.Common.MVVM;
using ColorVision.Themes;
using log4net;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Desktop.Wizards
{
    public class WizardManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WizardManager));
        private static WizardManager _instance;
        private static readonly object _locker = new();
        public static WizardManager GetInstance() { lock (_locker) { _instance ??= new WizardManager(); return _instance; } }
        public List<IWizardStep> IWizardSteps { get; private set; } = new();
        public List<IWizardInitializer> WizardInitializers { get; private set; } = new();

        public void Initialized()
        {
            IWizardSteps.Clear();
            WizardInitializers.Clear();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract))
                {
                    if (typeof(IWizardStep).IsAssignableFrom(type) && Activator.CreateInstance(type) is IWizardStep wizardStep)
                    {
                        log.Debug(type);
                        IWizardSteps.Add(wizardStep);
                    }

                    if (typeof(IWizardInitializer).IsAssignableFrom(type) && Activator.CreateInstance(type) is IWizardInitializer initializer)
                    {
                        log.Debug(type);
                        WizardInitializers.Add(initializer);
                    }
                }
            }
            IWizardSteps = IWizardSteps.OrderBy(handler => handler.Order).ToList();
            WizardInitializers = WizardInitializers.OrderBy(handler => handler.Order).ToList();
        }
    }

    /// <summary>
    /// WizardWindow.xaml interaction logic.
    /// </summary>
    public partial class WizardWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WizardWindow));

        public static WizardWindowConfig WindowConfig => WizardWindowConfig.Instance;

        private int _currentStepIndex;
        private List<IWizardStep> _wizardSteps = new();
        private INotifyPropertyChanged? _observedStep;
        private bool _initializersRun;
        private bool _isTransitioning;

        public WizardWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            WindowConfig.SetWindow(this);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            WizardManager.GetInstance().Initialized();
            _wizardSteps = WizardManager.GetInstance().IWizardSteps;
            ListWizard.ItemsSource = _wizardSteps;

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    if (!_wizardSteps.Any(step => step.IsRequired))
                    {
                        _initializersRun = true;
                        if (RunInitializers())
                            return;
                    }

                    if (_wizardSteps.Count > 0)
                    {
                        _currentStepIndex = 0;
                        await ShowCurrentStepAsync().ConfigureAwait(true);
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Failed to initialize the configuration wizard.", ex);
                    MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }), DispatcherPriority.Loaded);
        }

        private bool RunInitializers()
        {
            bool isFirstRun = !WindowConfig.WizardCompletionKey;
            foreach (IWizardInitializer initializer in WizardManager.GetInstance().WizardInitializers)
            {
                WizardInitializationContext context = new(this, isFirstRun);
                initializer.Initialize(context);
                if (context.SkipRequested)
                {
                    SkipWizardConfiguration();
                    return true;
                }
            }

            return false;
        }

        private async Task ShowCurrentStepAsync()
        {
            if (_wizardSteps.Count == 0)
                return;

            _isTransitioning = true;
            IWizardStep currentStep = _wizardSteps[_currentStepIndex];
            ObserveStep(currentStep);

            BorderContent.DataContext = currentStep;
            ListWizard.SelectedIndex = _currentStepIndex;
            ListWizard.ScrollIntoView(currentStep);
            WizardProgress.Value = (_currentStepIndex + 1) * 100.0 / _wizardSteps.Count;
            StepProgressText.Text = string.Format(
                Properties.Resources.Wizard_StepProgressFormat,
                _currentStepIndex + 1,
                _wizardSteps.Count);
            UpdateNavigationState();

            try
            {
                await currentStep.RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                log.Warn($"Wizard step refresh failed: {currentStep.GetType().FullName}", ex);
            }
            finally
            {
                _isTransitioning = false;
                UpdateNavigationState();
                await Dispatcher.InvokeAsync(() =>
                {
                    CurrentStepTitle.Focus();
                }, DispatcherPriority.Input);
            }
        }

        private void ObserveStep(IWizardStep step)
        {
            if (_observedStep != null)
                _observedStep.PropertyChanged -= CurrentStep_PropertyChanged;

            _observedStep = step as INotifyPropertyChanged;
            if (_observedStep != null)
                _observedStep.PropertyChanged += CurrentStep_PropertyChanged;
        }

        private void CurrentStep_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(UpdateNavigationState));
                return;
            }

            UpdateNavigationState();
        }

        private void UpdateNavigationState()
        {
            if (_wizardSteps.Count == 0)
                return;

            IWizardStep currentStep = _wizardSteps[_currentStepIndex];
            bool canNavigate = !_isTransitioning && currentStep.CanContinue;

            BtnPrevious.IsEnabled = !_isTransitioning && _currentStepIndex > 0;
            BtnNext.IsEnabled = canNavigate;
            BtnFinish.IsEnabled = canNavigate;
            RequiredStepHint.Visibility = currentStep.IsRequired && !currentStep.ConfigurationStatus
                ? Visibility.Visible
                : Visibility.Collapsed;

            bool isLastStep = _currentStepIndex == _wizardSteps.Count - 1;
            BtnNext.Visibility = isLastStep ? Visibility.Collapsed : Visibility.Visible;
            BtnFinish.Visibility = isLastStep ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_isTransitioning || _currentStepIndex <= 0)
                return;

            _currentStepIndex--;
            await ShowCurrentStepAsync().ConfigureAwait(true);
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_isTransitioning || _currentStepIndex >= _wizardSteps.Count - 1)
                return;

            IWizardStep currentStep = _wizardSteps[_currentStepIndex];
            if (!currentStep.CanContinue)
            {
                UpdateNavigationState();
                return;
            }

            int nextStepIndex = _currentStepIndex + 1;
            if (!_initializersRun && ShouldRunInitializersBefore(nextStepIndex))
            {
                _initializersRun = true;
                if (RunInitializers())
                    return;
            }

            _currentStepIndex = nextStepIndex;
            await ShowCurrentStepAsync().ConfigureAwait(true);
        }

        private bool ShouldRunInitializersBefore(int nextStepIndex)
        {
            if (nextStepIndex >= _wizardSteps.Count || _wizardSteps[nextStepIndex].IsRequired)
                return false;

            return _wizardSteps
                .Take(nextStepIndex)
                .Where(step => step.IsRequired)
                .All(step => step.ConfigurationStatus);
        }

        private async void ConfigurationComplete_Click(object sender, RoutedEventArgs e)
        {
            IWizardStep? incompleteRequiredStep = _wizardSteps.FirstOrDefault(step => step.IsRequired && !step.ConfigurationStatus);
            if (incompleteRequiredStep != null)
            {
                _currentStepIndex = _wizardSteps.IndexOf(incompleteRequiredStep);
                await ShowCurrentStepAsync().ConfigureAwait(true);
                return;
            }

            if (!_initializersRun)
            {
                _initializersRun = true;
                if (RunInitializers())
                    return;
            }

            bool result = _wizardSteps.All(item => item.ConfigurationStatus);
            WindowConfig.WizardCompletionKey = result;
            ConfigHandler.GetInstance().SaveConfigs();

            if (!result)
            {
                if (MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    Properties.Resources.SkipIncompleteConfigPrompt,
                    "ColorVision",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                WindowConfig.WizardCompletionKey = true;
                ConfigHandler.GetInstance().SaveConfigs();
            }

            CompleteWizard();
        }

        private void SkipWizardConfiguration()
        {
            WindowConfig.WizardCompletionKey = true;
            ConfigHandler.GetInstance().SaveConfigs();
            CompleteWizard();
        }

        private void CompleteWizard()
        {
            if (Application.Current.MainWindow == this)
            {
                Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"));
                Application.Current.Shutdown();
            }
            else
            {
                Close();
            }
        }
    }
}

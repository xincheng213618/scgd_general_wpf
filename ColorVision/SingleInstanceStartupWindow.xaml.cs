using ColorVision.Themes;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision;

public partial class SingleInstanceStartupWindow : Window
{
    private readonly SingleInstanceStartupCoordinator _coordinator;
    private readonly string _processDescription;
    private bool _started;
    private bool _allowClose;

    internal SingleInstanceStartupResult Result { get; private set; } = SingleInstanceStartupResult.Cancel;

    internal SingleInstanceStartupWindow(Func<IProgress<string>, CancellationToken, Task> forceCloseAsync, string processIds)
    {
        InitializeComponent();
        this.ApplyCaption();
        _processDescription = string.IsNullOrEmpty(processIds) ? "旧进程检查" : $"本次检查进程：{processIds}";
        ProcessesText.Text = _processDescription;
        _coordinator = new SingleInstanceStartupCoordinator(token =>
        {
            var progress = new Progress<string>(status =>
            {
                if (!_allowClose && !token.IsCancellationRequested && _coordinator.IsForcing)
                    ProcessesText.Text = status;
            });
            return forceCloseAsync(progress, token);
        });
        _coordinator.StateChanged += UpdateState;
        Loaded += Window_Loaded;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started || _allowClose)
            return;
        _started = true;
        try { Result = await _coordinator.RunAsync(); }
        finally
        {
            _allowClose = true;
            _coordinator.StateChanged -= UpdateState;
            Close();
        }
    }

    private void UpdateState()
    {
        StatusText.Text = _coordinator.Status;
        FailureText.Text = _coordinator.Detail;
        if (!string.IsNullOrEmpty(_coordinator.Detail))
            ProcessesText.Text = _processDescription;
        FailureText.Visibility = string.IsNullOrEmpty(_coordinator.Detail) ? Visibility.Collapsed : Visibility.Visible;
        ClosingProgress.Visibility = _coordinator.IsForcing ? Visibility.Visible : Visibility.Hidden;
        ForceButton.IsEnabled = !_coordinator.IsForcing;
    }

    private void Force_Click(object sender, RoutedEventArgs e) => _coordinator.Choose(SingleInstanceStartupChoice.ForceClose);
    private void Open_Click(object sender, RoutedEventArgs e) => _coordinator.Choose(SingleInstanceStartupChoice.OpenAdditional);
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose && _started)
        {
            e.Cancel = true;
            _coordinator.Choose(SingleInstanceStartupChoice.Cancel);
        }
        else if (!_started)
        {
            _allowClose = true;
            _coordinator.StateChanged -= UpdateState;
        }
        base.OnClosing(e);
    }
}

using ProjectARVRPro.Process;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProjectARVRPro.Controls;

public partial class ProcessStepProgress : UserControl
{
    private const double MinimumStepWidth = 136;
    public ObservableCollection<ProcessStepProgressItem> Steps { get; } = new();
    public Func<bool>? CanEditConfiguration { get; set; }
    public event Action<ProcessMeta>? TemplateRequested;
    public event Action<ProcessMeta>? ConfigurationRequested;

    public ProcessStepProgress() => InitializeComponent();

    public void Rebuild(IEnumerable<ProcessMeta> metas)
    {
        Steps.Clear();
        ProcessMeta[] enabled = metas.Where(meta => meta.IsEnabled).ToArray();
        for (int i = 0; i < enabled.Length; i++)
            Steps.Add(new ProcessStepProgressItem(enabled[i], i + 1, i == 0, i == enabled.Length - 1));
        UpdateWidth();
    }

    public void Reset()
    {
        foreach (ProcessStepProgressItem step in Steps) step.Reset();
        StepScroller.ScrollToLeftEnd();
    }

    public void Start(int index)
    {
        if (index < 0 || index >= Steps.Count) return;
        Steps[index].Start();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (StepItems.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
                container.BringIntoView();
        }));
    }

    public void UpdateElapsed(int index, long elapsedMilliseconds, long expectedMilliseconds)
    {
        if (index >= 0 && index < Steps.Count) Steps[index].UpdateElapsed(elapsedMilliseconds, expectedMilliseconds);
    }

    public void Finish(int index, bool succeeded)
    {
        if (index >= 0 && index < Steps.Count) Steps[index].Finish(succeeded);
    }

    private void ProgressLayout_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateWidth();

    private void UpdateWidth()
    {
        double stepsWidth = Steps.Count * MinimumStepWidth;
        bool overflow = ActualWidth > 0 && stepsWidth > ActualWidth;
        // Reserve both arrow slots while overflowing so reaching either end does not shift the steps.
        PreviousStepsButton.Visibility = NextStepsButton.Visibility = overflow ? Visibility.Visible : Visibility.Collapsed;
        double navigationWidth = overflow ? PreviousStepsButton.Width + NextStepsButton.Width : 0;
        StepItems.Width = Math.Max(Math.Max(0, ActualWidth - navigationWidth), stepsWidth);
        UpdateNavigation();
    }

    private void StepScroller_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateNavigation();

    private void UpdateNavigation()
    {
        PreviousStepsButton.IsEnabled = StepScroller.HorizontalOffset > 0;
        NextStepsButton.IsEnabled = StepScroller.HorizontalOffset < StepScroller.ScrollableWidth;
    }

    private void PreviousSteps_Click(object sender, RoutedEventArgs e) => StepScroller.ScrollToHorizontalOffset(StepScroller.HorizontalOffset - MinimumStepWidth);
    private void NextSteps_Click(object sender, RoutedEventArgs e) => StepScroller.ScrollToHorizontalOffset(StepScroller.HorizontalOffset + MinimumStepWidth);

    private void StepScroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (StepScroller.ScrollableWidth <= 0) return;
        StepScroller.ScrollToHorizontalOffset(StepScroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void Step_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not Button { Tag: ProcessStepProgressItem step }) return;
        TemplateRequested?.Invoke(step.Meta);
        e.Handled = true;
    }

    private void Step_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not Button { Tag: ProcessStepProgressItem step }) return;
        TemplateRequested?.Invoke(step.Meta);
        e.Handled = true;
    }

    private static ProcessStepProgressItem? MenuStep(ContextMenu menu) => (menu.PlacementTarget as Button)?.Tag as ProcessStepProgressItem;

    private void StepMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && menu.Items[1] is MenuItem edit)
            edit.IsEnabled = MenuStep(menu) != null && (CanEditConfiguration?.Invoke() ?? true);
    }

    private void OpenTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Parent: ContextMenu menu } && MenuStep(menu) is ProcessStepProgressItem step)
            TemplateRequested?.Invoke(step.Meta);
    }

    private void EditConfiguration_Click(object sender, RoutedEventArgs e)
    {
        if ((CanEditConfiguration?.Invoke() ?? true) && sender is MenuItem { Parent: ContextMenu menu } && MenuStep(menu) is ProcessStepProgressItem step)
            ConfigurationRequested?.Invoke(step.Meta);
    }
}

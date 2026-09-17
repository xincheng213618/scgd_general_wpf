using ColorVision.Themes;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI.Desktop.Feedback;

public partial class FeedbackDiagnosticsWindow : Window
{
    private readonly List<CollectorItem> _items;
    private readonly ListCollectionView _view;

    public FeedbackDiagnosticsWindow(IEnumerable<CollectorItem> items)
    {
        _items = items.ToList();
        _view = new ListCollectionView(_items);
        InitializeComponent();
        this.ApplyCaption();
        CollectorsList.ItemsSource = _view;
        foreach (CollectorItem item in _items)
            item.PropertyChanged += Item_PropertyChanged;
        UpdateSummary();
    }

    protected override void OnClosed(EventArgs e)
    {
        foreach (CollectorItem item in _items)
            item.PropertyChanged -= Item_PropertyChanged;
        base.OnClosed(e);
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        string search = SearchTextBox.Text.Trim();
        _view.Filter = value => value is CollectorItem item &&
            (item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)
             || item.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CollectorItem.IsChecked))
            UpdateSummary();
    }

    private void UpdateSummary() => SelectionSummaryText.Text = string.Format(
        Properties.Resources.FeedbackDiagnosticsSummary, _items.Count(item => item.IsChecked), _items.Count);

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (CollectorItem item in _items)
            item.IsChecked = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (CollectorItem item in _items)
            item.IsChecked = false;
    }

    private void RestoreDefaults_Click(object sender, RoutedEventArgs e)
    {
        foreach (CollectorItem item in _items)
            item.IsChecked = item.Collector.IsSelectedByDefault;
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}

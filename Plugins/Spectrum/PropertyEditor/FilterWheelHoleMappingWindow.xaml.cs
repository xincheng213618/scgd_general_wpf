using Spectrum.Configs;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum.PropertyEditor;

public partial class FilterWheelHoleMappingWindow : Window
{
    private readonly FilterWheelHoleMappingEditSession session;

    public FilterWheelHoleMappingWindow(IEnumerable<FilterWheelHoleMap>? mapping)
    {
        session = new FilterWheelHoleMappingEditSession(mapping);
        InitializeComponent();
        DataContext = session;
    }

    public ObservableCollection<FilterWheelHoleMap>? Result { get; private set; }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var row = session.AddRow();
        MappingGrid.SelectedItem = row;
        MappingGrid.ScrollIntoView(row);
        ValidationMessage.Text = string.Empty;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: FilterWheelHoleMappingRow row }) session.Rows.Remove(row);
        ValidationMessage.Text = string.Empty;
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (ValidationMessage != null) ValidationMessage.Text = string.Empty;
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (!session.TryCreateMapping(out var mapping, out string error))
        {
            ValidationMessage.Text = error;
            return;
        }
        Result = mapping;
        DialogResult = true;
    }
}

using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.UI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.PropertyEditor;

public sealed class FilterWheelLicensePropertiesEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        var panel = new DockPanel();
        var edit = new Button { Content = Properties.Resources.Edit, Margin = new Thickness(5, 0, 0, 0), MinWidth = 70 };
        DockPanel.SetDock(edit, Dock.Right);
        panel.Children.Add(edit);
        panel.Children.Add(PropertyEditorHelper.CreateLabel(property, PropertyEditorHelper.GetResourceManager(obj)));

        var combo = new HandyControl.Controls.ComboBox
        {
            Margin = new Thickness(5, 0, 0, 0), Style = PropertyEditorHelper.ComboBoxSmallStyle, IsEditable = true
        };
        HandyControl.Controls.InfoElement.SetShowClearButton(combo, true);
        combo.SetBinding(ComboBox.TextProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property.Name));
        TextSearch.SetTextPath(combo, nameof(LicenseViewModel.MacAddress));
        var template = TextSNPropertiesEditor.CreateDeviceSnItemTemplate(nameof(LicenseViewModel.LiceTypeString),
            nameof(LicenseViewModel.MacAddress), nameof(LicenseViewModel.StatusText), nameof(LicenseViewModel.StatusColor),
            statusForegroundPath: nameof(LicenseViewModel.StatusColor));
        combo.ItemTemplate = template;
        combo.ItemContainerStyle = TextSNPropertiesEditor.CreateCameraSnItemContainerStyle(template);

        async Task RefreshAsync()
        {
            try
            {
                // Service authorization looks up the shared license catalog by SN, not camera type.
                var licenses = await Task.Run(() => PhyLicenseDao.Instance.GetAll());
                string text = combo.Text;
                combo.ItemsSource = licenses.Where(license => license.LiceType is 0 or 1 && !string.IsNullOrWhiteSpace(license.MacAddress))
                    .Select(license => new LicenseViewModel(license)).ToArray();
                combo.SetCurrentValue(ComboBox.TextProperty, text);
                combo.ToolTip = null;
            }
            catch (Exception ex) { combo.ToolTip = ex.Message; }
        }

        combo.Loaded += async (_, _) => await RefreshAsync();
        edit.Command = new RelayCommand(async _ =>
        {
            new LicenseManagerWindow { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            await RefreshAsync();
        });
        panel.Children.Add(combo);
        return panel;
    }
}

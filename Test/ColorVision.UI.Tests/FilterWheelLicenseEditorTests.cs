using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Themes;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class FilterWheelLicenseEditorTests
{
    // Protect license selection and the saved SN without requiring camera registration or hardware.
    [Fact]
    public async Task CameraAndSpectrumLicensesCanBeSelectedWithoutChangingTheExistingSnOnLoad()
    {
        await WpfTestHost.Invoke(async () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), $"cfw-license-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            var previous = PhyLicenseDao.Instance;
            List<ResourceDictionary> dictionaries = [];
            try
            {
                PhyLicenseDao.Instance = new PhyLicenseDao(new LocalTemplateStore(Path.Combine(directory, "config.db")), () => false);
                PhyLicenseDao.Instance.Save(new LicenseModel { MacAddress = "CAMERA-1", LiceType = 0 });
                PhyLicenseDao.Instance.Save(new LicenseModel { MacAddress = "SPECTRUM-1", LiceType = 1 });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                var config = new ConfigCfwPort { SN = "EXISTING-SN" };
                var draft = CfwPortConfigurationDraft.From(config, new());
                var property = typeof(CfwPortConfigurationDraft).GetProperty(nameof(ConfigCfwPort.SN))!;
                Assert.Equal(typeof(FilterWheelLicensePropertiesEditor), property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
                var panel = new FilterWheelLicensePropertiesEditor().GenProperties(property, draft);
                var combo = panel.Children.OfType<ComboBox>().Single();
                var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var source = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, combo.GetType());
                EventHandler changed = (_, _) => loaded.TrySetResult();
                source.AddValueChanged(combo, changed);
                try
                {
                    combo.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally { source.RemoveValueChanged(combo, changed); }
                Assert.Equal("EXISTING-SN", draft.SN);
                Assert.Equal(2, combo.Items.Count);
                foreach (string serial in new[] { "CAMERA-1", "SPECTRUM-1" })
                {
                    combo.SelectedItem = combo.Items.Cast<LicenseViewModel>().Single(item => item.MacAddress == serial);
                    combo.GetBindingExpression(ComboBox.TextProperty)!.UpdateSource();
                    Assert.Equal(serial, draft.SN);
                }
                Assert.Equal("EXISTING-SN", config.SN);
                draft.Apply(config, new());
                Assert.Equal("SPECTRUM-1", config.SN);
            }
            finally
            {
                PhyLicenseDao.Instance = previous;
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
                Directory.Delete(directory);
            }
        });
    }
}

using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SensorTemplateSelectionTests
{
    [Fact]
    public void SwitchingSingleCommandTemplatesRefreshesFieldsAndEditsOnlyTheSelectedTemplate()
    {
        WpfTestHost.Invoke(() =>
        {
            ResourceDictionary previousResources = Application.Current.Resources;
            try
            {
                ResourceDictionary resources = new();
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                    resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
                Application.Current.Resources = resources;

                var firstModel = new ModDetailModel { ValueA = "41,42,Hex,1000/0,0" };
                var secondModel = new ModDetailModel { ValueA = "51,52,Hex,2000/10,1" };
                var first = new SensorParam { ModMaster = new ModMasterModel { Name = "First" }, SensorCommands = [new SensorCommand(firstModel)] };
                var second = new SensorParam { ModMaster = new ModMasterModel { Name = "Second" }, SensorCommands = [new SensorCommand(secondModel)] };
                var editor = new EditTemplateSensor();
                var selector = (ComboBox)editor.FindName("CommandSelector");
                var form = (StackPanel)editor.FindName("CommandForm");
                var request = (TextBox)editor.FindName("TextBoxSendCommand");
                var response = (TextBox)editor.FindName("TextBoxResCommand");

                editor.SetParam(first);
                Assert.Equal(0, selector.SelectedIndex);
                Assert.Same(first.SensorCommands[0], form.DataContext);

                editor.SetParam(second);
                Assert.Equal(0, selector.SelectedIndex);
                Assert.Same(second.SensorCommands[0], form.DataContext);
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal("51", request.Text);
                Assert.Equal("52", response.Text);

                response.Text = "53";
                response.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                Assert.Equal("41,42,Hex,1000/0,0", firstModel.ValueA);
                Assert.Equal("51,53,Hex,2000/10,1", secondModel.ValueA);

                editor.SetParam(first);
                Assert.Same(first.SensorCommands[0], form.DataContext);
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal("41", request.Text);
                Assert.Equal("42", response.Text);
            }
            finally
            {
                Application.Current.Resources = previousResources;
            }
        });
    }
}

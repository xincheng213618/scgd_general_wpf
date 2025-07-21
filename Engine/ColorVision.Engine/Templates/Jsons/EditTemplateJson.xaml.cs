using ColorVision.Common.Utilities;
using ColorVision.UI;
using HelixToolkit.Wpf;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Engine.Templates.Jsons
{

    public class EditTemplateJsonConfig :IConfig
    {
        public static EditTemplateJsonConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateJsonConfig>();

        public double Width { get; set; } = double.NaN;
    }

    public partial class EditTemplateJson : UserControl, ITemplateUserControl
    {
        private string Description { get; set; }
        public EditTemplateJson(string description)
        {
            Description = description;
            InitializeComponent();
            this.Width = EditTemplateJsonConfig.Instance.Width;
            this.SizeChanged += (s, e) =>
            {
                EditTemplateJsonConfig.Instance.Width = this.ActualWidth;
            };
            textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
            textEditor.ShowLineNumbers = true;
            textEditor.TextChanged += TextEditor_TextChanged;

        }

        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            DebounceTimer.AddOrResetTimer("EditTemplateJsonChanged", 50, EditTemplateJsonChanged);
        }

        public void EditTemplateJsonChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (IEditTemplateJson != null)
                {
                    IEditTemplateJson.JsonValue = textEditor.Text;
                }
            });
        }


        private IEditTemplateJson IEditTemplateJson;


        public void SetParam(object param)
        {
            if (param is IEditTemplateJson editTemplateJson)
            {
                this.DataContext = param; 
                if (IEditTemplateJson !=null)
                    IEditTemplateJson.JsonValueChanged -= IEditTemplateJson_JsonValueChanged;
                IEditTemplateJson = editTemplateJson;
                textEditor.Text = IEditTemplateJson.JsonValue;
                IEditTemplateJson.JsonValueChanged += IEditTemplateJson_JsonValueChanged;

                textEditor.TextChanged -= TextEditor_TextChanged;
                textEditor.TextChanged += TextEditor_TextChanged;

            }
            DescriptionButton.IsChecked = false;
        }

        private void IEditTemplateJson_JsonValueChanged(object? sender, EventArgs e)
        {
            textEditor.Text = IEditTemplateJson.JsonValue;
        }

        private string texttemp;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(sender is ToggleButton toggleButton)
            {
                if (toggleButton.IsChecked == true)
                {
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    texttemp = textEditor.Text;
                    textEditor.Text = Description;
                }
                else
                {

                    textEditor.Text = texttemp;
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    textEditor.TextChanged += TextEditor_TextChanged;
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.json.cn/",
                UseShellExecute = true
            });
            Common.NativeMethods.Clipboard.SetText(IEditTemplateJson.JsonValue);
        }
    }
}

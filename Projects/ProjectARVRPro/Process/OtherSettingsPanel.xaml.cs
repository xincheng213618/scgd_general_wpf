using ColorVision.UI;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ProjectARVRPro.Process;

public partial class OtherSettingsPanel : UserControl
{
    public OtherSettingsPanel() : this(ProjectARVRProConfig.Instance, () => ConfigService.Instance.Save<ProjectARVRProConfig>()) { }

    internal OtherSettingsPanel(ProjectARVRProConfig config, Action persist)
    {
        InitializeComponent();
        foreach (PropertyInfo property in PropertyEditorHelper.GetEditableProperties(typeof(ProjectARVRProConfig))
            .Where(property => property.GetCustomAttribute<CategoryAttribute>()?.Category == "LUX协议"))
        {
            var editor = PropertyEditorHelper.GenProperties(property, config);
            if (editor.Children.OfType<TextBlock>().FirstOrDefault() is { } label) label.Width = 190;
            FrameworkElement input = editor.Children.OfType<FrameworkElement>().First(child => child is TextBox or ComboBox);
            DependencyProperty target = input is TextBox ? TextBox.TextProperty : Selector.SelectedValueProperty;
            Binding binding = PropertyEditorHelper.CreateTwoWayBinding(config, property,
                input is TextBox ? UpdateSourceTrigger.LostFocus : UpdateSourceTrigger.PropertyChanged);
            binding.StringFormat = input.GetBindingExpression(target)?.ParentBinding.StringFormat;
            binding.ValidationRules.Add(new LuxSettingValidationRule(config, property.Name));
            binding.NotifyOnSourceUpdated = true;
            object? savedValue = property.GetValue(config);
            input.SourceUpdated += (_, _) =>
            {
                if (Equals(savedValue, property.GetValue(config))) return;
                try
                {
                    persist();
                    savedValue = property.GetValue(config);
                    StatusText.Text = string.Empty;
                }
                catch (Exception ex)
                {
                    property.SetValue(config, savedValue);
                    StatusText.Text = $"保存失败：{ex.Message}";
                }
            };
            input.SetBinding(target, binding);
            input.Width = double.NaN;
            input.MinHeight = 28;
            input.HorizontalAlignment = HorizontalAlignment.Stretch;
            editor.LastChildFill = true;
            SettingsContent.Children.Add(editor);
        }
    }

    internal void CommitPendingEdits()
    {
        foreach (var editor in SettingsContent.Children.OfType<DockPanel>())
            foreach (var input in editor.Children.OfType<TextBox>())
                input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private sealed class LuxSettingValidationRule(object source, string property) : ValidationRule(ValidationStep.ConvertedProposedValue, false)
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) =>
            ProjectSettingsSession.ValidateValue(source, property, value) is { } error ? new(false, error) : ValidationResult.ValidResult;
    }
}

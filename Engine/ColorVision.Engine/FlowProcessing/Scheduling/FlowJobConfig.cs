using ColorVision.Engine.Properties;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Scheduler;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace ColorVision.Engine.FlowProcessing.Scheduling;

public sealed class FlowJobConfig : JobConfigBase, IValidatableObject, IJobNameSuggestion
{
    [Display(Name = nameof(Resources.FlowTemplate), ResourceType = typeof(Resources))]
    [PropertyEditorType(typeof(FlowJobTemplateEditor))]
    public string? FlowKey
    {
        get => _flowKey;
        set => SetProperty(ref _flowKey, value);
    }
    private string? _flowKey;

    string? IJobNameSuggestion.SuggestedJobName => FindSavedTemplate(FlowKey)?.Key;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FindSavedTemplate(FlowKey) == null)
            yield return new ValidationResult(Resources.Flow_NoValidFlowTemplateSelected, [nameof(FlowKey)]);
    }

    internal static IEnumerable<TemplateModel<FlowParam>> SavedTemplates => TemplateFlow.Params.Where(template =>
        !string.IsNullOrWhiteSpace(template.Value.FlowKey) && !string.IsNullOrWhiteSpace(template.Value.DataBase64));

    internal static TemplateModel<FlowParam>? FindSavedTemplate(string? flowKey) => string.IsNullOrWhiteSpace(flowKey)
        ? null
        : SavedTemplates.FirstOrDefault(template => string.Equals(template.Value.FlowKey, flowKey, StringComparison.Ordinal));
}

public sealed class FlowJobTemplateEditor : IPropertyEditor
{
    public DockPanel GenProperties(PropertyInfo property, object obj)
    {
        TextBlock label = PropertyEditorHelper.CreateLabel(property, Resources.ResourceManager);
        var combo = new HandyControl.Controls.ComboBox
        {
            Margin = new Thickness(5, 0, 0, 0),
            Style = PropertyEditorHelper.ComboBoxSmallStyle,
            ItemsSource = FlowJobConfig.SavedTemplates.OrderBy(template => template.Key, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            DisplayMemberPath = nameof(TemplateModel<FlowParam>.Key),
            SelectedValuePath = $"{nameof(TemplateModel<FlowParam>.Value)}.{nameof(FlowParam.FlowKey)}",
            IsTextSearchEnabled = true
        };
        System.Windows.Automation.AutomationProperties.SetLabeledBy(combo, label);
        combo.SetBinding(Selector.SelectedValueProperty, PropertyEditorHelper.CreateTwoWayBinding(obj, property));

        var panel = new DockPanel();
        DockPanel.SetDock(label, Dock.Left);
        panel.Children.Add(label);
        panel.Children.Add(combo);
        return panel;
    }
}

using ColorVision.Engine.Templates;
using System;
using System.Collections;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.PropertyEditor;

internal static class TemplateSelectionDisplay
{
    internal sealed class MissingTemplate(string templateName)
    {
        public string TemplateName { get; } = templateName;
        public string Key => EngineLocalization.Format($"找不到模板：{TemplateName}");
    }

    internal static void Select(ComboBox combo, IEnumerable items, string? templateName)
    {
        object? selection = items.Cast<object>().FirstOrDefault(item => item is TemplateBase template && string.Equals(template.Key, templateName, StringComparison.Ordinal));
        combo.ItemsSource = items;
        if (selection == null && !string.IsNullOrWhiteSpace(templateName))
        {
            selection = new MissingTemplate(templateName);
            // Keep the live template collection and add the missing reference only to this control.
            combo.ItemsSource = new CompositeCollection { new CollectionContainer { Collection = items }, selection };
        }
        combo.SelectedItem = selection;
        combo.ToolTip = (selection as MissingTemplate)?.Key;
    }

    internal static string GetName(object? selection) => selection switch
    {
        TemplateBase template => template.Key,
        MissingTemplate missing => missing.TemplateName,
        _ => string.Empty
    };
}

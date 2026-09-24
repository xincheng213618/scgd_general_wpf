using ColorVision.Themes;
using ColorVision.UI;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Nodes;

public partial class LocalFindCrossConfigurationWindow : Window
{
    private LocalFindCrossConfigurationDraft? draft;
    private bool jsonMode;
    public string? ResultJson { get; private set; }
    private static readonly string[] BasicProperties =
    ["Name", "ExpectedAngle", "AngleTolerance", "FocusLength", "PixelSize", "UseImageCenter", "CenterX", "CenterY"];
    private static readonly string[] AdvancedProperties =
    ["UseOffset", "OffsetX", "OffsetY", "EnableDistortion", "K1", "K2", "P1", "P2", "K3", "Fx", "Fy", "Cx", "Cy"];

    public LocalFindCrossConfigurationWindow(string json)
    {
        InitializeComponent();
        this.ApplyCaption();
        JsonText.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
        JsonText.Text = json;
        if (LocalFindCrossConfigurationDraft.TryCreate(json, out draft, out string error))
        {
            BuildForm();
            if (!draft!.TryGetJson(out _, out error)) ErrorText.Text = error;
        }
        else { SetMode(true); ErrorText.Text = error; }
    }

    private void BuildForm()
    {
        BasicForm.Content = SettingsPropertyPresenter.Create(draft!, BasicProperties);
        AdvancedForm.Content = SettingsPropertyPresenter.Create(draft!, AdvancedProperties);
    }

    private void SetMode(bool useJson)
    {
        jsonMode = useJson;
        FormMode.IsChecked = !useJson;
        JsonMode.IsChecked = useJson;
        FormScroll.Visibility = useJson ? Visibility.Collapsed : Visibility.Visible;
        JsonText.Visibility = useJson ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FormMode_Click(object sender, RoutedEventArgs e)
    {
        if (!jsonMode) return;
        if (!LocalFindCrossConfigurationDraft.TryCreate(JsonText.Text, out var next, out string error))
        {
            SetMode(true);
            ErrorText.Text = error;
            return;
        }
        draft = next;
        BuildForm();
        SetMode(false);
        ErrorText.Text = draft!.TryGetJson(out _, out error) ? "" : error;
    }

    private void JsonMode_Click(object sender, RoutedEventArgs e)
    {
        if (jsonMode) return;
        if (!TryGetConfiguration(out string json)) { SetMode(false); return; }
        JsonText.Text = json;
        SetMode(true);
        ErrorText.Text = "";
    }

    internal bool TryGetConfiguration(out string json)
    {
        json = JsonText.Text;
        string error = "";
        if (jsonMode)
        {
            if (!LocalFindCrossConfigurationDraft.TryCreate(json, out var candidate, out error) || !candidate!.TryGetJson(out json, out error))
            { ErrorText.Text = error; return false; }
        }
        else
        {
            // Flush the focused field before validation, including a keyboard-triggered apply.
            foreach (TextBox box in Descendants(FormScroll).OfType<TextBox>()) box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            if (draft == null || !draft.TryGetJson(out json, out error))
            {
                ErrorText.Text = draft == null ? "请先修正 JSON 配置。" : error;
                if (draft != null) FocusInvalidField(draft.ErrorProperty);
                return false;
            }
        }
        ErrorText.Text = "";
        return true;
    }

    private void FocusInvalidField(string property)
    {
        if (AdvancedProperties.Contains(property)) AdvancedSection.IsExpanded = true;
        var box = Descendants(FormScroll).OfType<TextBox>().FirstOrDefault(b => b.GetBindingExpression(TextBox.TextProperty)?.ParentBinding.Path.Path == property);
        box?.BringIntoView();
        box?.Focus();
        box?.SelectAll();
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetConfiguration(out _)) ErrorText.Text = "配置有效。";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetConfiguration(out string json)) return;
        ResultJson = json;
        DialogResult = true;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}

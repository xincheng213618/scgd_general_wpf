using ColorVision.ImageEditor.Algorithms;
using ColorVision.Themes;
using ColorVision.UI;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms;

public partial class RgbCrossConfigurationWindow : Window
{
    private RgbCrossConfigurationDraft? draft;
    private bool jsonMode;
    private readonly bool includeJudgment;
    public RgbCrossRegistrationParameters? ResultParameters { get; private set; }
    public string? ResultJson { get; private set; }
    public RgbCrossConfigurationWindow(string json, bool includeJudgment = false)
    {
        this.includeJudgment = includeJudgment;
        InitializeComponent();
        JudgmentSection.Visibility = includeJudgment ? Visibility.Visible : Visibility.Collapsed;
        this.ApplyCaption();
        JsonText.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
        JsonText.Text = json;
        if (RgbCrossConfigurationDraft.TryCreate(json, out draft, out string error, includeJudgment))
        {
            BuildForm();
            if (!draft!.TryGetJson(out _, out error)) ErrorText.Text = error;
        }
        else { SetMode(true); ErrorText.Text = error; }
    }

    private void BuildForm()
    {
        BasicForm.Content = SettingsPropertyPresenter.Create(draft!, [nameof(draft.Rows), nameof(draft.Columns)]);
        AdvancedForm.Content = SettingsPropertyPresenter.Create(draft!, RgbCrossConfigurationDraft.Properties.Skip(2).ToArray());
        if (includeJudgment) JudgmentForm.Content = SettingsPropertyPresenter.Create(draft!, [nameof(draft.MaximumEdgeSeparationPixels)]);
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
        if (!RgbCrossConfigurationDraft.TryCreate(JsonText.Text, out var next, out string error, includeJudgment))
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
            if (!RgbCrossConfigurationDraft.TryCreate(json, out var candidate, out error, includeJudgment) || !candidate!.TryGetJson(out json, out error))
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
        if (property == nameof(RgbCrossConfigurationDraft.MaximumEdgeSeparationPixels)) JudgmentSection.IsExpanded = true;
        else if (property != nameof(RgbCrossConfigurationDraft.Rows) && property != nameof(RgbCrossConfigurationDraft.Columns)) AdvancedSection.IsExpanded = true;
        UpdateLayout();
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
        if (!RgbCrossConfigurationDraft.TryCreate(json, out var result, out _, includeJudgment)
            || !result!.TryGetParameters(out var parameters, out _)) return;
        ResultParameters = parameters;
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

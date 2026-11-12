using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using ColorVision.UI;
using ICSharpCode.AvalonEdit.Highlighting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Nodes;

public partial class LocalAutoExposureConfigurationWindow : Window
{
    private static readonly string[] BasicProperties =
        [nameof(LocalAutoExposureConfigurationDraft.StartTime), nameof(LocalAutoExposureConfigurationDraft.Saturation),
         nameof(LocalAutoExposureConfigurationDraft.MinTime), nameof(LocalAutoExposureConfigurationDraft.MaxTime)];
    private static readonly string[] AdvancedProperties =
        [nameof(LocalAutoExposureConfigurationDraft.MaxAd), nameof(LocalAutoExposureConfigurationDraft.SyncFrequency),
         nameof(LocalAutoExposureConfigurationDraft.MaxPercentage), nameof(LocalAutoExposureConfigurationDraft.SaturationDeviation),
         nameof(LocalAutoExposureConfigurationDraft.BurstThreshold)];

    private readonly string deviceJson;
    private LocalAutoExposureConfigurationDraft? draft;
    private bool followsDevice;
    private bool jsonMode;

    internal string ResultJson { get; private set; } = string.Empty;

    internal LocalAutoExposureConfigurationWindow(string json, PhyExpTimeCfg? deviceSettings)
    {
        InitializeComponent();
        this.ApplyCaption();
        JsonText.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("JavaScript");
        deviceJson = new JObject { ["expTimeCfg"] = JObject.FromObject(deviceSettings ?? new PhyExpTimeCfg()) }.ToString(Formatting.Indented);
        if (deviceSettings == null)
            HelpText.Text = EngineLocalization.Get("当前相机不可用。跟随设备时，表单预览软件默认值。");
        followsDevice = string.IsNullOrWhiteSpace(json);
        JsonText.Text = followsDevice ? deviceJson : json;
        if (!TryOpenForm()) SetMode(true);
    }

    private bool TryOpenForm(string status = "")
    {
        if (!LocalAutoExposureConfigurationDraft.TryCreate(JsonText.Text, out var next, out string error))
        {
            ShowStatus(error, isError: true);
            return false;
        }
        draft = next;
        BasicForm.Content = SettingsPropertyPresenter.Create(draft, BasicProperties);
        AdvancedForm.Content = SettingsPropertyPresenter.Create(draft, AdvancedProperties);
        SetMode(false);
        if (draft.TryGetJson(out _, out error)) ShowStatus(status);
        else ShowStatus(error, isError: true);
        return true;
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
        if (jsonMode && !TryOpenForm()) SetMode(true);
    }

    private void JsonMode_Click(object sender, RoutedEventArgs e)
    {
        if (jsonMode) return;
        if (!TryReadJson(out string json)) { SetMode(false); return; }
        JsonText.Text = json;
        SetMode(true);
    }

    private void UseDevice_Click(object sender, RoutedEventArgs e)
    {
        followsDevice = true;
        JsonText.Text = deviceJson;
        TryOpenForm(EngineLocalization.Get("载入设备参数，点击应用后恢复跟随设备。"));
    }

    internal bool TryGetConfiguration(out string json)
    {
        if (!TryReadJson(out json)) return false;
        if (followsDevice && json == deviceJson) json = string.Empty;
        return true;
    }

    private bool TryReadJson(out string json)
    {
        json = string.Empty;
        var candidate = draft;
        if (jsonMode)
        {
            if (!LocalAutoExposureConfigurationDraft.TryCreate(JsonText.Text, out candidate, out string parseError))
            {
                ShowStatus(parseError, isError: true);
                return false;
            }
        }
        else
        {
            var advancedForm = (UIElement)AdvancedForm.Content;
            var inputs = Descendants((UIElement)BasicForm.Content).Concat(Descendants(advancedForm)).OfType<TextBox>();
            foreach (TextBox box in inputs)
            {
                var binding = box.GetBindingExpression(TextBox.TextProperty);
                if (binding?.IsDirty == true) binding.UpdateSource();
                if (!Validation.GetHasError(box)) continue;

                if (advancedForm.IsAncestorOf(box)) AdvancedSection.IsExpanded = true;
                box.Focus();
                ShowStatus(EngineLocalization.Get("请修正输入框中的无效数值。"), isError: true);
                return false;
            }
        }
        if (!candidate!.TryGetJson(out json, out string error))
        {
            ShowStatus(error, isError: true);
            return false;
        }
        ShowStatus(string.Empty);
        return true;
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetConfiguration(out _)) ShowStatus(EngineLocalization.Get("配置有效。"));
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetConfiguration(out string json)) return;
        ResultJson = json;
        DialogResult = true;
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusText.Text = message;
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, isError ? "EditorSyntaxErrorBrush" : "SecondaryTextBrush");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}

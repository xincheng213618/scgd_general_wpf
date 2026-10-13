using ColorVision.Themes;
using ColorVision.UI;
using FlowEngineLib.Repeat;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing.Editor;

public partial class FlowRepeatCountWindow : Window
{
    private readonly RepeatSettings settings;
    private readonly TextBox input;
    public int Count => settings.Count;

    public FlowRepeatCountWindow(int count)
    {
        InitializeComponent();
        this.ApplyCaption();
        Title = FlowRepeatRegion.Text("重复执行…");
        settings = new RepeatSettings { Count = count };
        DockPanel field = PropertyEditorHelper.GenProperties(settings, nameof(RepeatSettings.Count));
        CountPanel.Children.Add(field);
        input = field.Children.OfType<TextBox>().Single();
        Explanation.Text = Properties.Resources.ResourceManager.GetString("总执行次数，包含第一次。中间结果覆盖，最后一次完成后再继续框外流程。")!;
        Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
    }

    internal bool ValidateCount()
    {
        input.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        bool valid = !Validation.GetHasError(input) && settings.Count >= 1;
        ErrorText.Text = valid ? string.Empty : FlowRepeatRegion.Text("执行次数必须是大于或等于 1 的整数。");
        ErrorText.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        return valid;
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateCount()) DialogResult = true;
    }

    public sealed class RepeatSettings
    {
        [Category("重复执行")]
        [DisplayName("执行次数")]
        [Description("总执行次数，包含第一次。中间结果覆盖，最后一次完成后再继续框外流程。")]
        public int Count { get; set; } = 10;
    }
}

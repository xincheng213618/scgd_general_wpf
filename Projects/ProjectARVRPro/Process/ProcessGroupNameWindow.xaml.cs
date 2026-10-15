using ColorVision.Themes;
using System.Windows;

namespace ProjectARVRPro.Process;

public partial class ProcessGroupNameWindow : Window
{
    private readonly ProcessManager _manager;
    private readonly ProcessGroup? _group;
    private readonly string _originalName;

    internal ProcessGroupNameWindow(ProcessManager manager, ProcessGroup? group)
    {
        _manager = manager;
        _group = group;
        _originalName = group?.Name ?? string.Empty;
        InitializeComponent();
        this.ApplyCaption();
        Title = DisplayText.Get(group == null ? "添加流程组" : "重命名流程组");
        HeadingText.Text = Title;
        DescriptionText.Text = DisplayText.Get(group == null
            ? "为新的产品或检测方案创建流程组。"
            : "修改组名，保留组内流程及其配置。");
        SaveButton.Content = DisplayText.Get(group == null ? "创建" : "保存");
        NameBox.Text = _originalName;
        NameBox.TextChanged += (_, _) => UpdateValidation(showError: true);
        UpdateValidation(showError: false);
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void UpdateValidation(bool showError)
    {
        string? error = _manager.GetGroupNameError(NameBox.Text, _group);
        ErrorText.Text = showError ? error ?? string.Empty : string.Empty;
        SaveButton.IsEnabled = error == null
            && (_group == null || !string.Equals(NameBox.Text.Trim(), _originalName, StringComparison.Ordinal));
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string error;
        bool saved = _group == null
            ? _manager.TryAddGroup(NameBox.Text, out error)
            : _manager.TryRenameGroup(_group, NameBox.Text, out error);
        if (!saved)
        {
            ErrorText.Text = error;
            return;
        }
        DialogResult = true;
    }
}

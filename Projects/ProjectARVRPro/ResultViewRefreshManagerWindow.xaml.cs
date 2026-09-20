using ColorVision.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ProjectARVRPro;

public partial class ResultViewRefreshManagerWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<ResultViewRefreshSettingItem> Items { get; }

    public bool HasAnyEnabled => Items.Any(item => item.IsWarning);
    public int EnabledCount => Items.Count(item => item.IsWarning);
    public string SummaryTitle => HasAnyEnabled
        ? $"有 {EnabledCount} 类已加载视图仍在自动刷新"
        : "所有已加载视图的自动刷新均已关闭";
    public string SummaryDescription => HasAnyEnabled
        ? "橙色项目会持续接收并显示新图像，长期运行时可能明显影响检测速度与稳定性。"
        : "当前没有已加载视图持续刷新；采集、算法执行和结果保存不受影响。";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ResultViewRefreshManagerWindow()
    {
        Items = ResultViewRefreshDiscovery.Discover();
        foreach (ResultViewRefreshSettingItem item in Items)
            item.PropertyChanged += Item_PropertyChanged;

        InitializeComponent();
        DataContext = this;
    }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(ResultViewRefreshSettingItem.IsAutoRefreshEnabled))
            return;

        OnPropertyChanged(nameof(HasAnyEnabled));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(SummaryTitle));
        OnPropertyChanged(nameof(SummaryDescription));
    }

    private void DisableAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (ResultViewRefreshSettingItem item in Items)
            item.IsAutoRefreshEnabled = false;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        foreach (ResultViewRefreshSettingItem item in Items)
            item.Apply();

        try
        {
            ConfigService.Instance.SaveConfigs();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            foreach (ResultViewRefreshSettingItem item in Items)
                item.Restore();

            MessageBox.Show(this,
                $"保存视图刷新配置失败，本次修改未应用：{ex.Message}",
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

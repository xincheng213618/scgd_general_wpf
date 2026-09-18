using ColorVision.Themes;
using Microsoft.Win32;
using QRCoder;
using QRCoder.Xaml;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Rbac.CloudDrive;

public partial class CloudDriveWindow : Window
{
    private static CloudDriveWindow? _window;
    private readonly CloudDriveManager _manager;
    private CloudDriveItem? _selected;

    public static void ShowCloudDrive()
    {
        _window ??= new CloudDriveWindow();
        if (Application.Current?.MainWindow is { IsVisible: true } mainWindow && mainWindow != _window && mainWindow is not RbacManagerWindow)
            _window.Owner = mainWindow;
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    public CloudDriveWindow(CloudDriveManager? manager = null)
    {
        _manager = manager ?? CloudDriveManager.Instance;
        InitializeComponent();
        DataContext = _manager;
        this.ApplyCaption();
        Loaded += async (_, _) => await _manager.RefreshCapabilitiesAsync();
        Closed += (_, _) =>
        {
            if (_selected != null) _selected.PropertyChanged -= Selected_Changed;
            _window = null;
        };
        Closing += (_, _) => { if (_manager.IsBusy) _manager.Pause(); };
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "选择要分享的文件", Filter = "所有文件|*.*" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = true, Title = "选择要打包分享的文件夹" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FolderNames);
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        try { _manager.AddPaths(paths); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = _manager.IsIdle && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Files_Drop(object sender, DragEventArgs e)
    {
        if (_manager.IsIdle && e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await _manager.UploadAsync();
    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (_selected != null) await _manager.UploadAsync(_selected);
    }
    private void Pause_Click(object sender, RoutedEventArgs e) => _manager.Pause();
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();

    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_selected != null) _selected.PropertyChanged -= Selected_Changed;
        _selected = FilesList.SelectedItem as CloudDriveItem;
        if (_selected != null) _selected.PropertyChanged += Selected_Changed;
        UpdateShare();
    }

    private void Selected_Changed(object? sender, PropertyChangedEventArgs e) => UpdateShare();

    private void UpdateShare()
    {
        bool canShare = _selected?.CanShare == true;
        CopyButton.IsEnabled = OpenButton.IsEnabled = canShare;
        QrBorder.Visibility = canShare ? Visibility.Visible : Visibility.Collapsed;
        string url = canShare ? _selected!.ShareUrl : "";
        if (LinkText.Text != url)
        {
            LinkText.Text = url;
            QrImage.Source = null;
            if (canShare)
            {
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
                using var qr = new XamlQRCode(data);
                var image = qr.GetGraphic(new Size(220, 220), Brushes.Black, Brushes.White, true);
                image.Freeze();
                QrImage.Source = image;
            }
        }
        ShareHint.Text = _selected == null ? "选择已完成的文件，即可生成分享二维码。"
            : canShare ? $"{_selected.DisplayName}\n{_selected.Detail}"
            : _selected.IsComplete ? "分享已过期，请重新添加文件并上传。" : "上传完成后会显示下载链接和二维码。";
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_selected?.CanShare != true) return;
        try { Clipboard.SetText(_selected.ShareUrl); ShareHint.Text = "分享链接已复制，可发送给其他人。"; }
        catch (Exception ex) { ShowError(ex); }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_selected?.CanShare != true) return;
        try { Process.Start(new ProcessStartInfo(_selected.ShareUrl) { UseShellExecute = true }); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        if (MessageBox.Show(this, "移除后将不再保留这条本机续传记录和分享链接。已发送的分享在到期前仍然有效。", "移除本机记录", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        try { _manager.Remove(_selected); }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowError(Exception ex) => MessageBox.Show(this, ex.Message, "云盘", MessageBoxButton.OK, MessageBoxImage.Warning);
}

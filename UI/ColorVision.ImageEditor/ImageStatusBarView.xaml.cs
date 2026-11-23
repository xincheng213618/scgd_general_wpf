using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Settings;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.ImageEditor;

public partial class ImageStatusBarView : UserControl
{
    private readonly string? _filePath;
    private readonly DispatcherTimer _hoverCloseTimer;
    private Window? _ownerWindow;
    private bool _detailsPinned;

    internal ImageStatusBarView(ImageStatusBarInfo info)
    {
        _filePath = info.FilePath;
        _hoverCloseTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(140)
        };
        _hoverCloseTimer.Tick += HoverCloseTimer_Tick;
        InitializeComponent();
        DataContext = info;
        RevealButton.Visibility = string.IsNullOrEmpty(_filePath) ? Visibility.Collapsed : Visibility.Visible;
        Loaded += ImageStatusBarView_Loaded;
        Unloaded += ImageStatusBarView_Unloaded;
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) CloseDetails();
        };
    }

    private void ImageStatusBarView_Loaded(object sender, RoutedEventArgs e)
    {
        _ownerWindow = Window.GetWindow(this);
        if (_ownerWindow != null) _ownerWindow.Deactivated += OwnerWindow_Deactivated;
    }

    private void ImageStatusBarView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_ownerWindow != null) _ownerWindow.Deactivated -= OwnerWindow_Deactivated;
        _ownerWindow = null;
        CloseDetails();
    }

    private void OwnerWindow_Deactivated(object? sender, EventArgs e) => CloseDetails();

    private void DetailsAnchor_MouseEnter(object sender, MouseEventArgs e)
    {
        _hoverCloseTimer.Stop();
        if (DetailsPopup.IsOpen) return;

        DetailsPopup.StaysOpen = true;
        DetailsPopup.IsOpen = true;
    }

    private void DetailsPanel_MouseEnter(object sender, MouseEventArgs e) => _hoverCloseTimer.Stop();

    private void DetailsArea_MouseLeave(object sender, MouseEventArgs e)
    {
        // Give the pointer time to cross the gap between the anchor and the popup.
        if (!_detailsPinned && DetailsPopup.IsOpen) _hoverCloseTimer.Start();
    }

    private void HoverCloseTimer_Tick(object? sender, EventArgs e)
    {
        _hoverCloseTimer.Stop();
        if (!_detailsPinned && !SummaryButton.IsMouseOver && !DetailsPanel.IsMouseOver) CloseDetails();
    }

    private void SummaryButton_Click(object sender, RoutedEventArgs e)
    {
        _hoverCloseTimer.Stop();
        _detailsPinned = true;
        DetailsPopup.StaysOpen = false;
        if (DetailsPopup.IsOpen)
            DetailsPanel.Focus();
        else
            DetailsPopup.IsOpen = true;
    }

    private void DetailsPopup_Opened(object? sender, EventArgs e)
    {
        if (_detailsPinned) DetailsPanel.Focus();
    }

    private void DetailsPopup_Closed(object? sender, EventArgs e) => ResetDetailsState();

    private void DetailsPanel_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_detailsPinned && !DetailsPanel.IsKeyboardFocusWithin) CloseDetails();
    }

    private void ResetDetailsState()
    {
        _hoverCloseTimer.Stop();
        _detailsPinned = false;
    }

    private void CloseDetails()
    {
        ResetDetailsState();
        DetailsPopup.IsOpen = false;
    }

    private void DetailsPanel_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        CloseDetails();
        SummaryButton.Focus();
        e.Handled = true;
    }

    private void RevealButton_Click(object sender, RoutedEventArgs e)
    {
        CloseDetails();
        if (File.Exists(_filePath))
        {
            PlatformHelper.OpenFolderAndSelectFile(_filePath);
            return;
        }

        MessageBox.Show(DisplayText.Get("图像文件不存在或无法访问。"), SettingsText.Information, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

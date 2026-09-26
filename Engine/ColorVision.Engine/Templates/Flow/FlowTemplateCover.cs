using ColorVision.Engine.Templates.Browser;
using log4net;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow;

/// <summary>Loads only covers intersecting the browser viewport; recycled/hidden requests cannot update it.</summary>
public sealed class FlowTemplateCover : Grid
{
    private static readonly ILog log = LogManager.GetLogger(typeof(FlowTemplateCover));
    public static readonly DependencyProperty FlowProperty = DependencyProperty.Register(nameof(Flow), typeof(FlowParam), typeof(FlowTemplateCover),
        new PropertyMetadata(null, (owner, _) => ((FlowTemplateCover)owner).Reset()));
    public FlowParam? Flow { get => (FlowParam?)GetValue(FlowProperty); set => SetValue(FlowProperty, value); }
    private readonly Image image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock placeholder = new() { Text = "正在生成预览…", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.LightGray, FontSize = 12 };
    private CancellationTokenSource? pending;
    private string? completedData, requestedData;
    private TemplateBrowserWindow? owner;

    public FlowTemplateCover()
    {
        Background = new SolidColorBrush(Color.FromRgb(29, 32, 37));
        Children.Add(placeholder);
        Children.Add(image);
        Loaded += (_, _) => { owner = Window.GetWindow(this) as TemplateBrowserWindow; owner?.RegisterCover(this); };
        Unloaded += (_, _) => { owner?.UnregisterCover(this); owner = null; };
    }

    private void Reset()
    {
        Cancel();
        completedData = null;
        image.Source = null;
        placeholder.Text = "正在生成预览…";
        placeholder.Visibility = Visibility.Visible;
        owner?.RegisterCover(this);
    }

    internal void Cancel()
    {
        pending?.Cancel();
        pending?.Dispose();
        pending = null;
        requestedData = null;
    }

    internal async void Refresh(FrameworkElement viewport, FlowTemplateCoverService service)
    {
        if (!IsLoaded || !IsVisible || Flow == null || !viewport.IsAncestorOf(this)) { Cancel(); return; }
        Rect bounds = TransformToAncestor(viewport).TransformBounds(new Rect(RenderSize));
        if (!bounds.IntersectsWith(new Rect(viewport.RenderSize))) { Cancel(); return; }
        string data = Flow.DataBase64;
        if (data == completedData || data == requestedData) return;
        Cancel();
        var request = new CancellationTokenSource();
        pending = request;
        requestedData = data;
        image.Source = null;
        placeholder.Text = "正在生成预览…";
        placeholder.Visibility = Visibility.Visible;
        ToolTip = null;
        try
        {
            var result = await service.LoadAsync(data, request.Token);
            if (!ReferenceEquals(pending, request) || Flow.DataBase64 != data) return;
            completedData = data;
            image.Source = result;
            placeholder.Text = "空流程";
            placeholder.Visibility = result == null ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ReferenceEquals(pending, request)) return;
            log.Warn("Flow cover unavailable; template editing remains available.", ex);
            completedData = data;
            placeholder.Text = "预览不可用";
            ToolTip = "仍可双击打开流程编辑器";
        }
        finally
        {
            if (ReferenceEquals(pending, request)) { pending = null; requestedData = null; }
            request.Dispose();
        }
    }
}

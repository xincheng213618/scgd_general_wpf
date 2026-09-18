using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

public sealed class SFREditorTool(ImageProcessingContext image, DrawEditorContext? draw = null)
{
    public void Execute() => _ = ExecuteAsync();

    private async Task ExecuteAsync()
    {
        try
        {
            if (draw == null) { SfrAnalysisRunner.Run(image, new RoiRect()); return; }
            var selection = await new TransientRoiSelectionSession(draw, SelectShapeType.Rectangle).Start();
            if (selection?.SourceScope is not { } scope || !TransientRoiSelectionSession.IsSourceScopeCurrent(image, scope)) return;
            RoiRect roi = SfrAnalysisRunner.PixelRoi(selection.Rect, scope);
            if (roi.Width > 0 && roi.Height > 0) SfrAnalysisRunner.Run(image, roi);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "斜边清晰度", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

internal static class SfrAnalysisRunner
{
    public static RoiRect PixelRoi(Rect rect, ImageSelectionScope scope)
    {
        int left = Math.Clamp((int)Math.Floor(rect.Left * scope.DpiX / 96), 0, scope.PixelWidth);
        int top = Math.Clamp((int)Math.Floor(rect.Top * scope.DpiY / 96), 0, scope.PixelHeight);
        int right = Math.Clamp((int)Math.Ceiling(rect.Right * scope.DpiX / 96), 0, scope.PixelWidth);
        int bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom * scope.DpiY / 96), 0, scope.PixelHeight);
        return new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    public static void Run(ImageProcessingContext context, RoiRect requested)
    {
        ImageFrameLease? lease = context.AcquireImageFrame();
        if (lease == null) return;
        try
        {
            HImage image = lease.Image;
            bool full = requested.X == 0 && requested.Y == 0 && requested.Width == 0 && requested.Height == 0;
            RoiRect roi = full ? new(0, 0, image.cols, image.rows) : requested;
            if (roi.Width <= 0 || roi.Height <= 0 || roi.X < 0 || roi.Y < 0 || roi.Width > image.cols || roi.Height > image.rows
                || roi.X > image.cols - roi.Width || roi.Y > image.rows - roi.Height)
                throw new ArgumentException("请选择图像内的一条完整斜边。");
            if ((long)roi.Width * roi.Height > 16_000_000 || roi.Width > 8192 || roi.Height > 8192)
                throw new ArgumentException("测量区域过大。请在主图框选一条斜边，在矩形右键菜单选择“斜边清晰度（SFR/MTF）”。");
            var window = new SfrSimplePlotWindow(lease, roi, context.DocumentInstanceId) { Owner = Application.Current.GetActiveWindow() };
            window.Show();
        }
        catch (Exception ex)
        {
            lease.Dispose();
            MessageBox.Show(ex.Message, "斜边清晰度", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public sealed class SFRIDVContextMenu : IDVContextMenu
{
    private readonly ImageProcessingContext _image;
    public SFRIDVContextMenu(ImageProcessingContext imageContext, ImageViewConfig config) => _image = imageContext;
    public Type ContextType => typeof(IRectangle);

    public IEnumerable<MenuItem> GetContextMenuItems(object obj)
    {
        if (obj is not IRectangle rectangle) return [];
        var item = new MenuItem { Header = "斜边清晰度（SFR/MTF）..." };
        item.Click += (_, _) =>
        {
            ImageSelectionScope? scope = TransientRoiSelectionSession.CaptureSourceScope(_image);
            if (scope == null) return;
            RoiRect roi = SfrAnalysisRunner.PixelRoi(rectangle.Rect, scope);
            if (roi.Width > 0 && roi.Height > 0) SfrAnalysisRunner.Run(_image, roi);
        };
        return [item];
    }
}


using LocalizedText = global::ColorVision.ImageEditor.DisplayText;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2
{
    public sealed record CMRotatedTemplateLocalAnalysis(
        ImageProcessingContext ImageContext,
        DrawEditorContext DrawContext,
        ImageViewConfig Config) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand command = new(_ => RotatedTemplateLocalAnalysis.Open(ImageContext, DrawContext, Config, default));
            return new List<MenuItemMetadata>
            {
                new()
                {
                    OwnerGuid = AlgorithmMenuGroups.Localization.Id,
                    GuidId = "P2RotatedTemplateLocalAnalysis",
                    Order = 4,
                    Header = ColorVision.ImageEditor.Properties.Resources.Algorithm_RotatedTemplate + "...",
                    Command = command
                }
            };
        }
    }

    public sealed class RotatedTemplateLocalAnalysisIDVContextMenu : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;

        public RotatedTemplateLocalAnalysisIDVContextMenu(
            ImageProcessingContext imageContext,
            DrawEditorContext drawContext,
            ImageViewConfig config)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
            _config = config;
        }

        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            using ImageFrameLease? lease = _imageContext.AcquireImageFrame();
            if (obj is not IRectangle rectangle || lease == null ||
                !P2RoiHelper.TryFromRectangle(rectangle, lease.Image, _config, out RoiRect roi))
            {
                return Array.Empty<MenuItem>();
            }

            MenuItem setTemplate = new() { Header = LocalizedText.Get("设为旋转匹配模板") };
            setTemplate.Click += (_, _) => SetTemplate(roi);
            MenuItem matchInRoi = new() { Header = LocalizedText.Get("在此 ROI 执行旋转模板匹配") };
            matchInRoi.Click += (_, _) => RotatedTemplateLocalAnalysis.Open(_imageContext, _drawContext, _config, roi);
            return new[] { setTemplate, matchInRoi };
        }

        private void SetTemplate(RoiRect roi)
        {
            try
            {
                using ImageFrameLease lease = _imageContext.AcquireImageFrame() ?? throw new InvalidOperationException("没有可用源图像。");
                TemplateMatchingImage template = TemplateMatchingImage.Capture(lease.Image, roi);
                RotatedTemplateSession session = RotatedTemplateSessionStore.Get(_drawContext);
                session.Template = template;
                session.Description = $"当前图像 ROI: {P2RoiHelper.Describe(roi)}";
                MessageBox.Show(
                    LocalizedText.Format($"模板已设置：{template.Width} x {template.Height}\n{session.Description}"),
                    ColorVision.ImageEditor.Properties.Resources.Algorithm_RotatedTemplate,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, LocalizedText.Get("设置模板失败"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    internal sealed class RotatedTemplateSession
    {
        public TemplateMatchingImage? Template { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    internal static class RotatedTemplateSessionStore
    {
        private static readonly ConditionalWeakTable<DrawEditorContext, RotatedTemplateSession> Sessions = new();

        public static RotatedTemplateSession Get(DrawEditorContext context) => Sessions.GetValue(context, _ => new RotatedTemplateSession());
    }

    internal static class RotatedTemplateLocalAnalysis
    {
        public static void Open(
            ImageProcessingContext imageContext,
            DrawEditorContext drawContext,
            ImageViewConfig config,
            RoiRect requestedRoi)
        {
            using ImageFrameLease? lease = imageContext.AcquireImageFrame();
            if (lease == null)
            {
                MessageBox.Show(LocalizedText.Get("当前没有可匹配的图像。"), ColorVision.ImageEditor.Properties.Resources.Algorithm_RotatedTemplate, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RotatedTemplateSession session = RotatedTemplateSessionStore.Get(drawContext);
            TemplateMatchingWindow window = new(imageContext, drawContext, config, requestedRoi, session.Template)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window.Show();
        }
    }
}
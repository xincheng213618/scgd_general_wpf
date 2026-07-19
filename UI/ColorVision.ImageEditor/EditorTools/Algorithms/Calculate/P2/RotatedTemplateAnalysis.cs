using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "P2RotatedTemplateLocalAnalysis",
                    Order = 6,
                    Header = "旋转模板本地匹配",
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
            if (obj is not IRectangle rectangle || _imageContext.HImageCache is not HImage image ||
                !P2RoiHelper.TryFromRectangle(rectangle, image, _config, out RoiRect roi))
            {
                return Array.Empty<MenuItem>();
            }

            MenuItem setTemplate = new() { Header = "设为旋转匹配模板" };
            setTemplate.Click += (_, _) => SetTemplate(roi);
            MenuItem matchInRoi = new() { Header = "在此 ROI 执行旋转模板匹配" };
            matchInRoi.Click += (_, _) => RotatedTemplateLocalAnalysis.Open(_imageContext, _drawContext, _config, roi);
            return new[] { setTemplate, matchInRoi };
        }

        private void SetTemplate(RoiRect roi)
        {
            try
            {
                BitmapSource template = P2RoiHelper.CropCurrentBitmap(_imageContext, roi);
                RotatedTemplateSession session = RotatedTemplateSessionStore.Get(_drawContext);
                session.Template = template;
                session.Description = $"当前图像 ROI: {P2RoiHelper.Describe(roi)}";
                MessageBox.Show(
                    $"模板已设置：{template.PixelWidth} x {template.PixelHeight}\n{session.Description}",
                    "旋转模板本地匹配",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "设置模板失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    internal sealed class RotatedTemplateSession
    {
        public BitmapSource? Template { get; set; }

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
            if (imageContext.HImageCache is not HImage image)
            {
                MessageBox.Show("当前没有可匹配的图像。", "旋转模板本地匹配", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RotatedTemplateSession session = RotatedTemplateSessionStore.Get(drawContext);
            if (session.Template == null)
            {
                MessageBox.Show(
                    "请先在图像上绘制矩形，右键该矩形并选择“设为旋转匹配模板”。",
                    "尚未设置模板",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            RoiRect roi = P2RoiHelper.Normalize(requestedRoi, image);
            P2JsonAnalysisWindow window = new(
                "旋转模板本地匹配",
                $"Search ROI: {P2RoiHelper.Describe(roi)}    Template: {session.Template.PixelWidth} x {session.Template.PixelHeight} ({session.Description})",
                CreateDefaultConfig(),
                json => RunAsync(imageContext, session.Template, roi, json),
                BuildSummary,
                drawContext,
                config,
                P2OverlayKind.TemplateMatching)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window.Show();
        }

        private static async Task<P2NativeResult> RunAsync(
            ImageProcessingContext context,
            BitmapSource template,
            RoiRect roi,
            string config)
        {
            if (context.HImageCache is not HImage current)
            {
                throw new InvalidOperationException("当前图像已经关闭或切换。");
            }

            IntPtr sourcePointer = current.pData;
            using P2ImageSnapshot sourceSnapshot = P2ImageSnapshot.Copy(current);
            using P2ImageSnapshot templateSnapshot = P2ImageSnapshot.FromBitmap(template);
            P2NativeResult result = await Task.Run(() => P2NativeJson.Invoke(
                "旋转模板本地匹配",
                (out IntPtr result) => OpenCVMediaHelper.M_MatchRotatedTemplate(
                    sourceSnapshot.Image,
                    templateSnapshot.Image,
                    roi,
                    config,
                    out result)));
            if (context.HImageCache is not HImage latest || latest.pData != sourcePointer)
            {
                throw new InvalidOperationException("计算期间当前图像发生了切换，结果已丢弃。");
            }
            return result;
        }

        private static string CreateDefaultConfig()
        {
            var config = new
            {
                angleMin = -15.0,
                angleMax = 15.0,
                angleStep = 1.0,
                scaleMin = 0.90,
                scaleMax = 1.10,
                scaleStep = 0.05,
                featureMode = "gradient",
                occlusionTolerance = 0.20,
                scoreThreshold = 0.80,
                maxMatches = 10,
                nmsRadius = 12.0,
                pyramidLevels = 2,
                subpixel = true
            };
            return JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        private static string BuildSummary(JObject result)
        {
            JObject? best = result["matches"]?.First as JObject;
            return string.Format(
                CultureInfo.InvariantCulture,
                "匹配: {0}    候选: {1}    Best Score: {2:F3}    Angle: {3:F1}°    Scale: {4:F2}",
                (result["matches"] as JArray)?.Count ?? 0,
                result.Value<int?>("candidateCount") ?? 0,
                best?.Value<double?>("score") ?? 0.0,
                best?.Value<double?>("angleDegrees") ?? 0.0,
                best?.Value<double?>("scale") ?? 1.0);
        }
    }
}

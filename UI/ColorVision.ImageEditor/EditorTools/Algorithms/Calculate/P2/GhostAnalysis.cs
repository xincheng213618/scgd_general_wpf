using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.P2
{
    public sealed record CMGhostLocalAnalysis(
        ImageProcessingContext ImageContext,
        DrawEditorContext DrawContext,
        ImageViewConfig Config) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand command = new(_ => GhostLocalAnalysis.Open(ImageContext, DrawContext, Config, default));
            return new List<MenuItemMetadata>
            {
                new()
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "P2GhostLocalAnalysis",
                    Order = 5,
                    Header = "Ghost 本地分析",
                    Command = command
                }
            };
        }
    }

    public sealed class GhostLocalAnalysisIDVContextMenu : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;

        public GhostLocalAnalysisIDVContextMenu(
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

            MenuItem item = new() { Header = "在此 ROI 执行 Ghost 本地分析" };
            item.Click += (_, _) => GhostLocalAnalysis.Open(_imageContext, _drawContext, _config, roi);
            return new[] { item };
        }
    }

    internal static class GhostLocalAnalysis
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
                MessageBox.Show("当前没有可分析的图像。", "Ghost 本地分析", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HImage image = lease.Image;
            RoiRect roi = P2RoiHelper.Normalize(requestedRoi, image);
            P2JsonAnalysisWindow window = new(
                "Ghost 本地分析",
                $"Image: {image.cols} x {image.rows}    ROI: {P2RoiHelper.Describe(roi)}",
                CreateDefaultConfig(),
                imageContext,
                json => RunAsync(imageContext, roi, json),
                BuildSummary,
                drawContext,
                config,
                P2OverlayKind.Ghost)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window.Show();
        }

        private static async Task<P2NativeResult> RunAsync(ImageProcessingContext context, RoiRect roi, string config)
        {
            using ImageFrameLease? lease = context.AcquireImageFrame();
            if (lease == null)
            {
                throw new InvalidOperationException("当前图像已经关闭或切换。");
            }

            long revision = lease.Revision;
            P2NativeResult result = await Task.Run(() => P2NativeJson.Invoke(
                "Ghost 本地分析",
                (out IntPtr result) => OpenCVMediaHelper.M_DetectGhosts(lease.Image, roi, config, out result)));
            if (!context.IsCurrentImageRevision(revision))
            {
                throw new InvalidOperationException("计算期间当前图像发生了切换，结果已丢弃。");
            }
            return result;
        }

        private static string CreateDefaultConfig()
        {
            var config = new
            {
                channel = -1,
                brightThresholdMode = "auto",
                ghostThresholdMode = "auto",
                brightGridRows = 1,
                brightGridCols = 1,
                brightMinArea = 16,
                brightMaxArea = 0,
                ghostMinArea = 4,
                ghostMaxArea = 200000,
                sourceMaskPadding = 3,
                minDistanceFromBright = 10.0,
                minRelativeIntensity = 0.0,
                maxCandidates = 128,
                normalizeExposure = false,
                exposureLowPercentile = 0.01,
                exposureHighPercentile = 0.995,
                backgroundKernel = 0,
                backgroundSigma = 0.0,
                multiScaleLevels = 1,
                multiScaleFactor = 1.6,
                multiScaleThresholdFactor = 0.85,
                useDirectionalConfidence = false,
                minDirectionConfidence = 0.0
            };
            return JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        private static string BuildSummary(JObject result)
        {
            JObject? summary = result["summary"] as JObject;
            return string.Format(
                CultureInfo.InvariantCulture,
                "亮源: {0}    Ghost: {1}    Max Severity: {2:F3}    Mean Confidence: {3:F3}",
                summary?.Value<int?>("brightSourceCount") ?? 0,
                summary?.Value<int?>("candidateCount") ?? 0,
                summary?.Value<double?>("maxSeverity") ?? 0.0,
                summary?.Value<double?>("meanConfidence") ?? 0.0);
        }
    }
}

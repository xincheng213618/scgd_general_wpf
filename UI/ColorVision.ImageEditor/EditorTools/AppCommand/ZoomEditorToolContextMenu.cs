using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{


    public record class ZoomEditorToolContextMenu(ImageViewConfig config, DrawEditorContext drawContext) : IIEditorToolContextMenu, ICopilotBusinessContextSource
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            PublishCopilotContext();

            var MenuItemMetadatas = new  List<MenuItemMetadata>();
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenImage", Order = 10, Header = Properties.Resources.Open, Command = ApplicationCommands.Open , Icon = MenuItemIcon.TryFindResource("DIOpen") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "SaveAsImage", Order = 300, Header = Properties.Resources.SaveAsImage, Command = ApplicationCommands.SaveAs ,Icon = MenuItemIcon.TryFindResource("DISave") });

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "ClearImage", Order = 11, Header = Properties.Resources.Clear, Command = ApplicationCommands.Close, Icon = MenuItemIcon.TryFindResource("DIDelete") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Print", Order = 300, Header = Properties.Resources.Print, Command = ApplicationCommands.Print, Icon = MenuItemIcon.TryFindResource("DIPrint"), InputGestureText = "Ctrl+P" });

            RelayCommand askCopilotImageCommand = new RelayCommand(a => AskCopilotAboutImage());
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "AskCopilotAboutImage", Order = 310, Header = "问 AI 分析当前图像", Command = askCopilotImageCommand });

            return MenuItemMetadatas;
        }

        private void AskCopilotAboutImage()
        {
            var bundle = CaptureCopilotContext();
            var result = CopilotBusinessContextCoordinator.DispatchDiagnosis(
                bundle,
                "请基于已附加的图像元数据、选区/ROI 和标注摘要，分析当前图像可能需要关注的质量问题、测量风险和下一步检查建议。注意：当前上下文不包含图像像素，只能基于结构化信息判断。");

            if (!result.WasSent)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), result.StatusMessage, "ColorVision", MessageBoxButton.OK,
                    result.IsAvailable ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
        }

        private void PublishCopilotContext()
        {
            try
            {
                CopilotBusinessContextCoordinator.Publish(CaptureCopilotContext());
            }
            catch
            {
            }
        }

        public CopilotBusinessContextBundle CaptureCopilotContext()
        {
            var snapshot = BuildImageSnapshot();
            var contextItem = CopilotBusinessContextBuilder.BuildImageContextItem(snapshot);
            return CopilotBusinessContextBundle.FromItem(snapshot.SourceId, contextItem);
        }

        private CopilotImageContextSnapshot BuildImageSnapshot()
        {
            var metadata = config.GetPropertyEntries()
                .OrderBy(entry => entry.Scope)
                .ThenBy(entry => entry.Key, System.StringComparer.Ordinal)
                .Select(entry => new CopilotContextProperty
                {
                    Name = string.IsNullOrWhiteSpace(entry.Owner) ? entry.Key : $"{entry.Key} ({entry.Owner})",
                    Value = ImageViewConfig.FormatPropertyValue(entry.Value),
                })
                .ToArray();

            var annotations = drawContext.DrawingVisualLists?
                .Select(DescribeDrawingVisual)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(30)
                .ToArray() ?? System.Array.Empty<string>();

            var selectedRegions = drawContext.SelectionVisual?.SelectVisuals?
                .Select(DescribeSelectedRegion)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(20)
                .ToArray() ?? System.Array.Empty<string>();

            return new CopilotImageContextSnapshot
            {
                SourceId = $"image-editor:{drawContext.Id:N}",
                Title = "Current image editor image",
                ImagePath = GetPropertyValue(ImageViewPropertyKeys.FilePath),
                FileName = GetPropertyValue(ImageViewPropertyKeys.FileName),
                FileSize = GetPropertyValue(ImageViewPropertyKeys.FileSize),
                ImageSize = BuildImageSize(),
                PixelFormat = GetPropertyValue(ImageViewPropertyKeys.PixelFormat),
                Channel = GetPropertyValue(ImageViewPropertyKeys.Channel),
                Depth = GetPropertyValue(ImageViewPropertyKeys.Depth),
                Dpi = BuildDpi(),
                Metadata = metadata,
                SelectedRegions = selectedRegions,
                AnnotationCount = drawContext.DrawingVisualLists?.Count ?? 0,
                AnnotationSummaries = annotations,
            };
        }

        private string GetPropertyValue(string key)
        {
            return config.Properties.TryGetValue(key, out var value)
                ? ImageViewConfig.FormatPropertyValue(value)
                : string.Empty;
        }

        private string BuildImageSize()
        {
            var width = FirstNonEmpty(GetPropertyValue(ImageViewPropertyKeys.ImageWidth), GetPropertyValue(ImageViewPropertyKeys.Cols));
            var height = FirstNonEmpty(GetPropertyValue(ImageViewPropertyKeys.ImageHeight), GetPropertyValue(ImageViewPropertyKeys.Rows));
            return !string.IsNullOrWhiteSpace(width) && !string.IsNullOrWhiteSpace(height)
                ? $"{width} x {height}"
                : string.Empty;
        }

        private string BuildDpi()
        {
            var dpiX = GetPropertyValue(ImageViewPropertyKeys.DpiX);
            var dpiY = GetPropertyValue(ImageViewPropertyKeys.DpiY);
            return !string.IsNullOrWhiteSpace(dpiX) && !string.IsNullOrWhiteSpace(dpiY)
                ? $"{dpiX} x {dpiY}"
                : string.Empty;
        }

        private static string DescribeDrawingVisual(IDrawingVisual visual)
        {
            var attribute = visual.BaseAttribute;
            var label = string.IsNullOrWhiteSpace(attribute?.Name) ? visual.GetType().Name : attribute.Name;
            var parts = new List<string> { label };

            if (attribute != null && attribute.Id != 0)
                parts.Add($"Id={attribute.Id}");
            if (!string.IsNullOrWhiteSpace(attribute?.Msg))
                parts.Add($"Msg={attribute.Msg}");
            if (visual is ISelectVisual selectVisual)
            {
                var rect = selectVisual.GetRect();
                if (!rect.IsEmpty)
                    parts.Add($"Rect=({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})");
            }

            return string.Join("; ", parts);
        }

        private static string DescribeSelectedRegion(ISelectVisual visual)
        {
            var rect = visual.GetRect();
            var name = visual.GetType().Name;
            return rect.IsEmpty
                ? name
                : $"{name} Rect=({rect.X:0.##},{rect.Y:0.##},{rect.Width:0.##},{rect.Height:0.##})";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }

    public record class ImageViewSettingsEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand openSettingsCommand = new RelayCommand(a => context.OpenSettingsWindow(Properties.Resources.Settings_GroupContext));

            return new List<MenuItemMetadata>
            {
                new MenuItemMetadata() { GuidId = "ImageViewSettings", Order = 303, Header = "设置", Command = openSettingsCommand, Icon = MenuItemIcon.TryFindResource("DIExpand") },
            };
        }
    }

}

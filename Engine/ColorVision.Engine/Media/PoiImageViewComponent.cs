using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Media
{
    public class PoiImageViewComponent : IImageComponent
    {
        public const string SelectedTemplateRuntimeKey = "POI.SelectedTemplate";
        public const string IsTemplateSupportedRuntimeKey = "POI.IsTemplateSupported";

        public static bool TryGetSelectedTemplate(ImageView imageView, out PoiParam poiParam)
        {
            poiParam = imageView.Config.GetProperties<PoiParam>(SelectedTemplateRuntimeKey);


            return poiParam != null && poiParam.Id != -1;
        }

        public void Execute(ImageView imageView)
        {
            ComboBox? poiTemplateComboBox = null;
            SelectionChangedEventHandler? selectionChangedHandler = null;
            List<Visual> poiTemplateVisuals = new();
            int loadVersion = 0;

            imageView.ImageShow.ImageInitialized += (_, _) => RefreshPoiTemplateUi();
            imageView.Config.Cleared += (_, _) =>
            {
                ClearPoiTemplateVisuals();
                RemovePoiTemplateUi(clearSelection: false);
            };

            void RefreshPoiTemplateUi()
            {
                if (!IsPoiTemplateSupported(imageView))
                {
                    RemovePoiTemplateUi(clearSelection: true);
                    return;
                }

                EnsurePoiTemplateUi();
                LoadTemplatesAsync(++loadVersion);
            }

            void EnsurePoiTemplateUi()
            {
                if (poiTemplateComboBox != null)
                {
                    return;
                }

                poiTemplateComboBox = new ComboBox
                {
                    Width = 100,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    DisplayMemberPath = "Key",
                    SelectedValuePath = "Value",
                    Style = Application.Current.TryFindResource("ComboBox.Small") as Style
                };

                selectionChangedHandler = (_, _) => ApplySelectedTemplate(imageView, poiTemplateComboBox, ClearPoiTemplateVisuals, poiTemplateVisuals);
                poiTemplateComboBox.SelectionChanged += selectionChangedHandler;
                imageView.ToolBarAl.Items.Add(poiTemplateComboBox);
            }

            void RemovePoiTemplateUi(bool clearSelection)
            {
                loadVersion++;
                ClearPoiTemplateVisuals();
                if (clearSelection)
                {
                    imageView.Config.SetViewState(SelectedTemplateRuntimeKey, null, nameof(PoiImageViewComponent), "当前选择的 POI 模板");
                }

                if (poiTemplateComboBox == null)
                {
                    return;
                }

                if (selectionChangedHandler != null)
                {
                    poiTemplateComboBox.SelectionChanged -= selectionChangedHandler;
                }

                if (imageView.ToolBarAl.Items.Contains(poiTemplateComboBox))
                {
                    imageView.ToolBarAl.Items.Remove(poiTemplateComboBox);
                }

                poiTemplateComboBox.ItemsSource = null;
                poiTemplateComboBox = null;
                selectionChangedHandler = null;
            }

            void ClearPoiTemplateVisuals()
            {
                foreach (Visual visual in poiTemplateVisuals)
                {
                    imageView.ImageShow.RemoveVisual(visual);
                }

                poiTemplateVisuals.Clear();
            }

            void LoadTemplatesAsync(int version)
            {
                if (MySqlControl.GetInstance().IsConnect)
                {
                    PopulateTemplates(version);
                    return;
                }

                Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await MySqlControl.GetInstance().Connect();
                    Application.Current.Dispatcher.Invoke(() => PopulateTemplates(version));
                });
            }

            void PopulateTemplates(int version)
            {
                if (version != loadVersion || poiTemplateComboBox == null || !IsPoiTemplateSupported(imageView))
                {
                    return;
                }

                if (!MySqlControl.GetInstance().IsConnect)
                {
                    return;
                }

                new TemplatePoi().Load();
                poiTemplateComboBox.ItemsSource = TemplatePoi.Params.CreateEmpty();
                poiTemplateComboBox.SelectedIndex = 0;
            }
        }

        private static bool IsPoiTemplateSupported(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string extension = Path.GetExtension(filePath);
            return extension.Equals(".cvraw", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".cvcie", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPoiTemplateSupported(ImageView imageView)
        {
            return imageView.Config.GetProperties<bool>(IsTemplateSupportedRuntimeKey)
                || IsPoiTemplateSupported(imageView.Config.FilePath);
        }

        private static void ApplySelectedTemplate(ImageView imageView, ComboBox comboBox, Action clearPoiTemplateVisuals, List<Visual> poiTemplateVisuals)
        {
            if (comboBox.SelectedValue is not PoiParam poiParams)
            {
                return;
            }

            clearPoiTemplateVisuals();
            imageView.Config.SetViewState(SelectedTemplateRuntimeKey, poiParams, nameof(PoiImageViewComponent), "当前选择的 POI 模板");
            if (poiParams.Id == -1)
            {
                return;
            }

            PoiParam.LoadPoiDetailFromDB(poiParams);
            foreach (var item in poiParams.PoiPoints)
            {
                switch (item.PointType)
                {
                    case GraphicTypes.Circle:
                        DVCircleText circle = new();
                        circle.Attribute.Center = new Point(item.PixX, item.PixY);
                        circle.Attribute.Radius = item.PixHeight / 2;
                        circle.Attribute.Brush = Brushes.Transparent;
                        circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                        circle.Attribute.Id = item.Id;
                        circle.Attribute.Text = item.Name;
                        circle.Render();
                        imageView.ImageShow.AddVisual(circle);
                        poiTemplateVisuals.Add(circle);
                        break;
                    case GraphicTypes.Rect:
                        DVRectangleText rectangle = new();
                        rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth / 2, item.PixY - item.PixHeight / 2, item.PixWidth, item.PixHeight);
                        rectangle.Attribute.Brush = Brushes.Transparent;
                        rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                        rectangle.Attribute.Id = item.Id;
                        rectangle.Attribute.Text = item.Name;
                        rectangle.Render();
                        imageView.ImageShow.AddVisual(rectangle);
                        poiTemplateVisuals.Add(rectangle);
                        break;
                    case GraphicTypes.Quadrilateral:
                        break;
                }
            }
        }
    }
}

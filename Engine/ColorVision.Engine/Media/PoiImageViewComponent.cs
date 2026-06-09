using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using System;
using System.Linq;
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
        public static readonly DependencyProperty IsTemplateSelectorEnabledProperty = DependencyProperty.RegisterAttached(
            "IsTemplateSelectorEnabled",
            typeof(bool),
            typeof(PoiImageViewComponent),
            new PropertyMetadata(true));

        public static bool GetIsTemplateSelectorEnabled(DependencyObject element)
        {
            return (bool)element.GetValue(IsTemplateSelectorEnabledProperty);
        }

        public static void SetIsTemplateSelectorEnabled(DependencyObject element, bool value)
        {
            element.SetValue(IsTemplateSelectorEnabledProperty, value);
        }

        public static bool TryGetSelectedTemplate(ImageView imageView, out PoiParam poiParam)
        {
            poiParam = imageView.Config.GetProperties<PoiParam>(SelectedTemplateRuntimeKey);


            return poiParam != null && poiParam.Id != -1;
        }

        public void Execute(ImageView imageView)
        {
            ComboBox? poiTemplateComboBox = null;
            SelectionChangedEventHandler? selectionChangedHandler = null;
            int loadVersion = 0;

            imageView.Dispatcher.BeginInvoke(() =>
            {
                if (!GetIsTemplateSelectorEnabled(imageView))
                {
                    return;
                }

                EnsurePoiTemplateUi();
                LoadTemplatesAsync(++loadVersion);
            });

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

                selectionChangedHandler = (_, _) => ApplySelectedTemplate(imageView, poiTemplateComboBox, ClearDrawingVisuals);
                poiTemplateComboBox.SelectionChanged += selectionChangedHandler;
                imageView.ToolBarAl.Items.Add(poiTemplateComboBox);
            }

            void ClearDrawingVisuals()
            {
                imageView.ImageShow.ClearActionCommand();
                foreach (Visual visual in imageView.EditorContext.DrawingVisualLists.OfType<Visual>().ToList())
                {
                    imageView.ImageShow.RemoveVisual(visual);
                }
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
                if (version != loadVersion || poiTemplateComboBox == null)
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

        private static void ApplySelectedTemplate(ImageView imageView, ComboBox comboBox, Action clearDrawingVisuals)
        {
            if (comboBox.SelectedValue is not PoiParam poiParams)
            {
                return;
            }

            clearDrawingVisuals();
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
                        break;
                    case GraphicTypes.Quadrilateral:
                        break;
                }
            }
        }
    }
}

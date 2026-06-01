#pragma warning disable CS0414,CS8625
using ColorVision.Common.MVVM;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class RectangleManagerConfig : ViewModelBase
    {
        [DisplayName("连续模式")]
        public bool IsContinuous { get => _IsContinuous; set { _IsContinuous = value; OnPropertyChanged(); } }
        private bool _IsContinuous;

        public bool IsLocked { get => _IsLocked; set { _IsLocked = value; OnPropertyChanged(); } }
        private bool _IsLocked;

        public bool UseCenter { get => _UseCenter; set { _UseCenter = value; OnPropertyChanged(); } }
        private bool _UseCenter;

        public double DefalutWidth { get => _DefalutWidth; set { _DefalutWidth = value; OnPropertyChanged(); } }
        private double _DefalutWidth = 30;

        public double DefalutHeight { get => _DefalutHeight; set { _DefalutHeight = value; OnPropertyChanged(); } }
        private double _DefalutHeight = 30;
    }

    public class RectangleManager : DragDrawingToolBase
    {
        public RectangleManagerConfig Config { get; set; } = new RectangleManagerConfig();
        private DVRectangleText? DrawingRectangleCache;

        public RectangleManager(DrawEditorContext context) : base(context)
        {
            Order = 4;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageRect");
        }

        protected override bool IgnoreWhenCtrlPressed => true;

        protected override IEnumerable<CompactInspectorItem> BuildCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.IsContinuous), Icon = CompactInspectorIcons.CreateText("∞"), Order = 10, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_ContinuousDraw },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.IsLocked), Icon = CompactInspectorIcons.CreateGlyph("\uE72E"), Order = 20, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_LockDefaultSize },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.UseCenter), Icon = CompactInspectorIcons.CreateText("◎"), Order = 30, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_CreateFromCenter },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.DefalutWidth), Label = "W", ShowLabel = true, Width = 56, Order = 40, EditorKind = CompactInspectorEditorKind.Number },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.DefalutHeight), Label = "H", ShowLabel = true, Width = 56, Order = 50, EditorKind = CompactInspectorEditorKind.Number },
            };
        }

        protected override void OnDeactivated()
        {
            DrawingRectangleCache = null;
        }

        protected override void OnBeginDraw(Point startPoint, MouseButtonEventArgs e)
        {
            if (DrawingRectangleCache != null)
            {
                e.Handled = true;
                return;
            }

            int did = GetNextDrawingVisualId();
            RectangleTextProperties rectangleTextProperties = new RectangleTextProperties
            {
                Id = did,
                Pen = new Pen(Brushes.Red, 1 / Zoombox.ContentMatrix.M11),
                Text = "Point_" + did,
            };

            if (Config.UseCenter)
            {
                rectangleTextProperties.Rect = new Rect(
                    new Point(startPoint.X + Config.DefalutWidth / 2, startPoint.Y + Config.DefalutHeight / 2),
                    new Point(startPoint.X - Config.DefalutWidth / 2, startPoint.Y - Config.DefalutHeight / 2));
            }
            else
            {
                rectangleTextProperties.Rect = new Rect(startPoint, new Point(startPoint.X + Config.DefalutWidth, startPoint.Y + Config.DefalutHeight));
            }

            DrawingRectangleCache = new DVRectangleText(rectangleTextProperties);
            DrawCanvas.AddVisualCommand(DrawingRectangleCache);
            e.Handled = true;
        }

        protected override void OnUpdateDraw(Point currentPoint, MouseEventArgs e)
        {
            if (DrawingRectangleCache != null)
            {
                DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownPoint, currentPoint);
                DrawingRectangleCache.Render();
            }
        }

        protected override void OnEndDraw(Point endPoint, MouseButtonEventArgs e)
        {
            if (DrawingRectangleCache != null)
            {
                SelectVisual(DrawingRectangleCache);
                if (DrawingRectangleCache.Attribute.Rect.Width == Config.DefalutWidth && DrawingRectangleCache.Attribute.Rect.Height == Config.DefalutHeight)
                {
                    DrawingRectangleCache.Render();
                }

                if (!Config.IsLocked)
                {
                    Config.DefalutWidth = DrawingRectangleCache.Attribute.Rect.Width;
                    Config.DefalutHeight = DrawingRectangleCache.Attribute.Rect.Height;
                }

                if (!Config.IsContinuous)
                {
                    IsChecked = false;
                }

                DrawingRectangleCache = null;
            }

            e.Handled = true;
        }
    }
}




#pragma warning disable CS0414,CS8625
using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class CircleManagerConfig : ViewModelBase
    {
        [DisplayName("连续模式")]
        public bool IsContinuous { get => _IsContinuous; set { _IsContinuous = value; OnPropertyChanged(); } }
        private bool _IsContinuous;

        public bool IsLocked { get => _IsLocked; set { _IsLocked = value; OnPropertyChanged(); } }
        private bool _IsLocked;

        public double DefalutRadius { get => _DefalutRadius; set { _DefalutRadius = value; OnPropertyChanged(); } }
        private double _DefalutRadius = 30;
    }

    public class CircleManager : DragDrawingToolBase
    {
        public CircleManagerConfig Config { get; set; } = new CircleManagerConfig();

        public CircleManager(DrawEditorContext context) : base(context)
        {
            Order = 3;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageCircle");
        }

        private DVCircleText? DrawCircleCache;

        protected override bool IgnoreWhenCtrlPressed => true;

        protected override IEnumerable<CompactInspectorItem> BuildCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.IsContinuous), Icon = CompactInspectorIcons.CreateText("∞"), Order = 10, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_ContinuousDraw },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.IsLocked), Icon = CompactInspectorIcons.CreateGlyph("\uE72E"), Order = 20, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_LockDefaultRadius },
                new CompactInspectorPropertyItem { Source = Config, PropertyName = nameof(Config.DefalutRadius), Label = "R", ShowLabel = true, Width = 56, Order = 30, EditorKind = CompactInspectorEditorKind.Number },
            };
        }

        protected override void OnDeactivated()
        {
            DrawCircleCache = null;
        }

        protected override void OnBeginDraw(Point startPoint, MouseButtonEventArgs e)
        {
            if (DrawCircleCache != null)
            {
                e.Handled = true;
                return;
            }

            ClearCurrentSelection();

            CircleTextProperties circleTextProperties = new CircleTextProperties();
            int did = GetNextDrawingVisualId();
            circleTextProperties.Id = did;
            circleTextProperties.Pen = new Pen(Brushes.Red, 1 / Zoombox.ContentMatrix.M11);
            circleTextProperties.Center = startPoint;
            circleTextProperties.Radius = Config.DefalutRadius;
            circleTextProperties.Text = "Point_" + did;
            DrawCircleCache = new DVCircleText(circleTextProperties);
            DrawCanvas.AddVisualCommand(DrawCircleCache);
            e.Handled = true;
        }

        protected override void OnUpdateDraw(Point currentPoint, MouseEventArgs e)
        {
            if (DrawCircleCache != null)
            {
                double radius = Math.Sqrt((Math.Pow(currentPoint.X - MouseDownPoint.X, 2) + Math.Pow(currentPoint.Y - MouseDownPoint.Y, 2)));
                DrawCircleCache.Attribute.Radius = radius;
                DrawCircleCache.Render();
            }
        }

        protected override void OnEndDraw(Point endPoint, MouseButtonEventArgs e)
        {
            if (DrawCircleCache != null)
            {
                SelectVisual(DrawCircleCache);
                if (DrawCircleCache.Attribute.Radius == Config.DefalutRadius)
                {
                    DrawCircleCache.Render();
                }

                if (!Config.IsLocked)
                {
                    Config.DefalutRadius = DrawCircleCache.Radius;
                }

                if (!Config.IsContinuous)
                {
                    IsChecked = false;
                }

                DrawCircleCache = null;
            }

            e.Handled = true;
        }
    }
}

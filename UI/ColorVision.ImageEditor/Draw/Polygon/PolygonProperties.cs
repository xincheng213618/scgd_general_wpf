using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class PolygonProperties : RegionProperties, ICompactInspectorProvider
    {
        [Browsable(false)]
        public Pen Pen
        {
            get => _Pen;
            set
            {
                _Pen = value ?? new Pen(Brushes.Red, 1);
                OnPropertyChanged();
                OnPropertyChanged(nameof(Brush));
                OnPropertyChanged(nameof(StrokeThickness));
            }
        }
        private Pen _Pen = new Pen(Brushes.Red, 1);


        [DisplayName("颜色")]
        public Brush Brush
        {
            get => Pen?.Brush ?? Brushes.Red;
            set
            {
                Brush next = value ?? Brushes.Red;
                Pen writablePen = EnsureWritablePen();
                if (Equals(writablePen.Brush, next))
                {
                    return;
                }

                writablePen.Brush = next;
                OnPropertyChanged();
            }
        }

        [DisplayName("线宽")]
        public double StrokeThickness
        {
            get => Pen?.Thickness ?? 1;
            set
            {
                if (!double.IsFinite(value))
                    return;

                double next = value < 1 ? 1 : value;
                Pen writablePen = EnsureWritablePen();
                if (writablePen.Thickness == next)
                {
                    return;
                }

                writablePen.Thickness = next;
                OnPropertyChanged();
            }
        }

        [DisplayName("闭合区域")]
        public bool IsClosed
        {
            get => _isClosed;
            set { if (_isClosed == value) return; _isClosed = value; InvalidateMeasurementMessage(); OnPropertyChanged(); }
        }
        private bool _isClosed;

        public List<Point> Points
        {
            get => _points;
            set { _points = value ?? new(); InvalidateMeasurementMessage(); OnPropertyChanged(); }
        }
        private List<Point> _points = new();

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem { Source = this, PropertyName = nameof(this.Rotation), Label = "θ°", ShowLabel = true, Width = 65, Order = 45, EditorKind = CompactInspectorEditorKind.Number, ToolTip = "旋转角度" },
                new CompactInspectorPropertyItem { Source = this, PropertyName = nameof(IsClosed), Label = "闭合", ShowLabel = true, Order = 0, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = "闭合区域可计算 POI" },
                new CompactInspectorPropertyItem { Source = this, PropertyName = nameof(Brush), Order = 10, EditorKind = CompactInspectorEditorKind.Brush, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_LineColor },
                new CompactInspectorPropertyItem { Source = this, PropertyName = nameof(StrokeThickness), Icon = CompactInspectorIcons.CreateText("━"), Width = 56, Order = 20, EditorKind = CompactInspectorEditorKind.Number, ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_LineWidth },
            };
        }

        private Pen EnsureWritablePen()
        {
            if (_Pen == null)
            {
                _Pen = new Pen(Brushes.Red, 1);
            }
            else if (_Pen.IsFrozen)
            {
                _Pen = _Pen.Clone();
            }

            return _Pen;
        }

    }



}

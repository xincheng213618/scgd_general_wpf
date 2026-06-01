using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class PolygonProperties : BaseProperties, ICompactInspectorProvider
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

        public List<Point> Points { get; set; }

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems(EditorContext context)
        {
            return new CompactInspectorItem[]
            {
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

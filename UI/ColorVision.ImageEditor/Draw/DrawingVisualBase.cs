using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DrawingVisualBase : DrawingVisual, INotifyPropertyChanged, ISelectVisual
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public virtual BaseProperties BaseAttribute { get; }
        public virtual int ID { get; set; }
        protected double? LayoutBasePenThickness { get; set; }
        protected double? LayoutBaseFontSize { get; set; }
        public virtual void Render() { }

        protected bool ApplyLayoutScaleCore(DrawingVisualScaleContext context, Pen? currentPen, Action<Pen>? assignPen = null, double? currentFontSize = null, Action<double>? assignFontSize = null)
        {
            bool isRender = false;

            if (currentPen != null)
            {
                LayoutBasePenThickness ??= currentPen.Thickness > 0 ? currentPen.Thickness : 1;

                Pen pen = currentPen;
                if (pen.IsFrozen)
                {
                    pen = pen.Clone();
                    assignPen?.Invoke(pen);
                }

                double targetThickness = context.IsLayoutUpdated
                    ? context.Scale
                    : context.TextFontSizeOverride > 0 ? context.TextFontSizeOverride / 10 : LayoutBasePenThickness.Value;
                if (pen.Thickness != targetThickness)
                {
                    pen.Thickness = targetThickness;
                    isRender = true;
                }
            }

            if (currentFontSize is double fontSize && assignFontSize != null)
            {
                LayoutBaseFontSize ??= fontSize > 0 ? fontSize : (LayoutBasePenThickness ?? 1) * 10;

                double targetFontSize = context.IsLayoutUpdated
                    ? context.Scale * 10
                    : context.TextFontSizeOverride > 0 ? context.TextFontSizeOverride : LayoutBaseFontSize.Value;
                if (fontSize != targetFontSize)
                {
                    assignFontSize(targetFontSize);
                    isRender = true;
                }
            }

            if (isRender)
                Render();

            return isRender;
        }

        public virtual Rect GetRect() => new Rect();

        public virtual void SetRect(Rect rect)
        {

        }

    }

    public class DrawingVisualBase<T>: DrawingVisualBase where T : BaseProperties, new()
    {
        public override BaseProperties BaseAttribute => Attribute;

        public override int ID { get => Attribute.Id; set => Attribute.Id = value; }

        public object Tag { get; set; }

        public object ToolTip { get; set; }

        public T Attribute { get; set; }
    }

    public class DrawingVisualBaseDVContextMenu : IDVContextMenu
    {
        private readonly DrawEditorContext _drawContext;

        public DrawingVisualBaseDVContextMenu(DrawEditorContext drawContext)
        {
            _drawContext = drawContext;
        }

        public Type ContextType => typeof(DrawingVisualBase);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            List<MenuItem> MenuItems = new List<MenuItem>();
            if (obj is DrawingVisualBase visual)
            {
                MenuItem menuIte2 = new MenuItem() { Header = ColorVision.ImageEditor.Properties.Resources.Draw_Delete };
                menuIte2.Click += (s, e) =>
                {
                    _drawContext.DrawCanvas.RemoveVisualCommand(visual);
                    _drawContext.SelectionVisual.ClearRender();
                };
                MenuItems.Add(menuIte2);

                MenuItem menuIte3 = new MenuItem() { Header = "Top" };
                menuIte3.Click += (s, e) =>
                {
                    _drawContext.DrawCanvas.TopVisual(visual);
                };
                MenuItems.Add(menuIte3);

                MenuItem menuItem4 = new MenuItem() { Header = ColorVision.ImageEditor.Properties.Resources.Draw_Edit };
                menuItem4.Click += (s, e) =>
                {
                    new PropertyEditorWindow(visual.BaseAttribute) { Owner =Application.Current.GetActiveWindow(),WindowStartupLocation =WindowStartupLocation.CenterOwner}.ShowDialog();
                };
                MenuItems.Add(menuItem4);
            }
            return MenuItems;
        }
    }
}

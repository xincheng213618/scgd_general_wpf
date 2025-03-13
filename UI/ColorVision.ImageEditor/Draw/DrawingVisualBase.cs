#pragma warning disable CA1711,CA2211
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DrawingVisualBase : DrawingVisual, INotifyPropertyChanged, ISelectVisual
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public static int No = 1;

        public virtual int ID { get; set; }

        public virtual void Render() { }

        public virtual Rect GetRect() => new Rect();

        public virtual void SetRect(Rect rect)
        {

        }

    }

    public class DrawingVisualBase<T>: DrawingVisualBase where T : BaseProperties, new()
    {
        public BaseProperties BaseAttribute => Attribute;

        public override int ID { get => Attribute.Id; set => Attribute.Id = value; }

        public bool IsShowText { get; set; } = true;


        public object Tag { get; set; }

        public object ToolTip { get; set; }

        public T Attribute { get; set; }
    }
}

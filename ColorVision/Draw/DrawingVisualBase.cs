#pragma warning disable CA1711,CA2211
using ColorVision.Device;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ColorVision
{
    public class DrawingVisualBase : DrawingVisual, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public static int No = 1;

        public virtual void Render() { }
    }

    public class DrawingVisualBase<T>: DrawingVisualBase where T : DrawBaseAttribute, new()
    {
        public T Attribute { get; set; }
    }



}

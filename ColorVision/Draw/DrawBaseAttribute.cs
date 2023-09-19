#pragma warning disable CA1711,CA2211
using System.ComponentModel;
using System.Windows.Media;
using ColorVision.MVVM;

namespace ColorVision
{

    public class DrawBaseAttribute : BaseAttribute
    {
        [Category("DrawingVisual"), DisplayName("序号")]
        public int ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private int _ID;

        [Category("DrawingVisual"), DisplayName("名称")]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        [Category("DrawingVisual"), DisplayName("是否显示")]
        public bool IsShow { get => _IsShow; set { _IsShow = value; NotifyPropertyChanged(); } }
        private bool _IsShow = true;

        [Category("DrawingVisual"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }
        private Pen _Pen;

    }



}

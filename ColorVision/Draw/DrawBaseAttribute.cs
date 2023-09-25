#pragma warning disable CA1711,CA2211
using System.ComponentModel;
using System.Windows.Media;
using ColorVision.MVVM;

namespace ColorVision
{

    public class DrawBaseAttribute : BaseAttribute
    {
        [Category("DrawBaseAttribute"), DisplayName("序号")]
        public int ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private int _ID;

        [Category("DrawBaseAttribute"), DisplayName("名称")]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        [Category("DrawBaseAttribute"), DisplayName("是否显示")]
        public bool IsShow { get => _IsShow; set { _IsShow = value; NotifyPropertyChanged(); } }
        private bool _IsShow = true;
    }



}

using ColorVision.Common.MVVM;
using System.ComponentModel;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{


    public class BaseProperties : ViewModelBase
    {
        public static SolidColorBrush DefaultBrush { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#01F3F3F3"));


        [Category("Attribute"), DisplayName("序号")]
        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;

        [Category("Attribute"), DisplayName("名称")]
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        [Category("Attribute"), DisplayName("是否显示")]
        public bool IsShow { get => _IsShow; set { _IsShow = value; OnPropertyChanged(); } }
        private bool _IsShow = true;

        [Browsable(false)]
        public int? Tag { get => _Tag; set { _Tag = value; OnPropertyChanged(); } }
        private int? _Tag;

        [Browsable(false)]
        public string? Msg { get => _Msg; set { _Msg = value; OnPropertyChanged(); } }
        private string? _Msg;

        [Browsable(false)]
        public object? Param { get => _Param; set { _Param = value; OnPropertyChanged(); } }
        private object? _Param;

    }



}

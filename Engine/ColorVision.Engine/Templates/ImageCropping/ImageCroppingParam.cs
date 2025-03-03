#pragma warning disable CA1707,IDE1006,CS8603
using ColorVision.Common.MVVM;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.ImageCropping
{
    public class PointFloat:ViewModelBase
    {
        public float X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private float _X;
        public float Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private float _Y;
    }

    public class ImageCroppingParam : ParamModBase
    {
        public ImageCroppingParam() { }
        public ImageCroppingParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("UnEgde"), Description("UnEgde")]
        public int UnEgde { get => GetValue(_UnEgde); set { SetProperty(ref _UnEgde, value); } }
        private int _UnEgde = 1;


        [Category("O_Index"), Description("O_Index")]
        public string O_Index { get => GetValue(_O_Index); set { SetProperty(ref _O_Index, value); } }
        private string _O_Index;


    }
}

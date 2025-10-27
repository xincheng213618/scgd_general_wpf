using ColorVision.Common.MVVM;
using ProjectLUX.Fix;
using System.ComponentModel;

namespace ProjectLUX.Process.VID
{
    public class VIDFixConfig : ViewModelBase, IFixConfig
    {
        [Category("VID")]
        public double VID { get => _VID; set { _VID = value; OnPropertyChanged(); } }
        private double _VID = 1;
    }

}
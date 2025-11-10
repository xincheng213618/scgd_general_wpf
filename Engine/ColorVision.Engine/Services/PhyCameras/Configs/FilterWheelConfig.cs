using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class FilterWheelConfig : ViewModelBase
    {
        public int HoleNum { get => _HoleNum; set { _HoleNum = value; OnPropertyChanged(); } }
        private int _HoleNum;

        public ObservableCollection<HoleMap> HoleMapping { get => _HoleMapping; set { _HoleMapping = value; OnPropertyChanged(); } }
        private ObservableCollection<HoleMap> _HoleMapping = new ObservableCollection<HoleMap>() {
            new HoleMap() { HoleIndex = 0, HoldName = "ND0" }, 
            new HoleMap() { HoleIndex = 1, HoldName = "ND10" }, 
            new HoleMap() { HoleIndex = 2, HoldName = "ND100" }, 
            new HoleMap() { HoleIndex = 3, HoldName = "ND1000" }, 
            new HoleMap() { HoleIndex = 4, HoldName = "EMPTY" } };
    }

    public class HoleMap : ViewModelBase
    {
        public int HoleIndex { get => _HoleIndex; set { _HoleIndex = value; OnPropertyChanged(); } }
        private int _HoleIndex;

        public string HoldName { get => _HoldName; set { _HoldName = value; OnPropertyChanged(); } }
        private string _HoldName;
    }


}
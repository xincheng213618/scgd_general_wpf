using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class FilterWheelConfig : ViewModelBase
    {

        public FilterWheelConfig() 
        {
            if (HoleMapping == null)
            {
                HoleMapping = new ObservableCollection<HoleMap>()
        {
            new HoleMap { HoleIndex = 0, HoleName = "ND0" },
            new HoleMap { HoleIndex = 1, HoleName = "ND10" },
            new HoleMap { HoleIndex = 2, HoleName = "ND100" },
            new HoleMap { HoleIndex = 3, HoleName = "ND1000" },
            new HoleMap { HoleIndex = 4, HoleName = "EMPTY" },
        };   
            }
        }

        public int HoleNum { get => _HoleNum; set { _HoleNum = value; OnPropertyChanged(); } }
        private int _HoleNum;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<HoleMap> HoleMapping
        {
            get => _HoleMapping ;
            set{ _HoleMapping = value; OnPropertyChanged(); } 
        }
        private ObservableCollection<HoleMap> _HoleMapping = null;
    }

    public class HoleMap : ViewModelBase
    {
        public int HoleIndex { get => _HoleIndex; set { _HoleIndex = value; OnPropertyChanged(); } }
        private int _HoleIndex;

        // 修正拼写: HoldName -> HoleName
        public string HoleName { get => _HoleName; set { _HoleName = value; OnPropertyChanged(); } }
        private string _HoleName = string.Empty;

        public HoleMap Clone() => new() { HoleIndex = HoleIndex, HoleName = HoleName };
    }
}
#pragma warning disable CA1805,CS8625
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class FilterWheelConfig : ViewModelBase
    {

        public FilterWheelConfig()
        {
            HoleMapping ??= new ObservableCollection<HoleMap>
            {
                new HoleMap { HoleIndex = 1, HoleName = "ND8" },
                new HoleMap { HoleIndex = 2, HoleName = "ND64" },
                new HoleMap { HoleIndex = 3, HoleName = "ND1000" },
                new HoleMap { HoleIndex = 4, HoleName = "EMPTY" },
                new HoleMap { HoleIndex = 5, HoleName = "Spectrum" },
            };
        }

        public int HoleNum { get => _HoleNum; set { _HoleNum = value; OnPropertyChanged(); } }
        private int _HoleNum;

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public ObservableCollection<HoleMap> HoleMapping
        {
            get => _HoleMapping;
            set { _HoleMapping = value; OnPropertyChanged(); }
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

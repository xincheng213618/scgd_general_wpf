using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class FilterWheelConfig : ViewModelBase
    {
        /// <summary>
        /// Predefined filter options available for selection
        /// </summary>
        public static readonly List<string> FilterOptions = new()
        {
            "IR", "ND0", "ND8", "ND10", "ND16", "ND32", "ND64", "ND1000",
            "X", "Y", "Z", "SPEC", "EMPTY"
        };

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

        /// <summary>
        /// Number of positions per wheel (default: 6)
        /// </summary>
        public int PositionsPerWheel { get => _PositionsPerWheel; set { _PositionsPerWheel = value; OnPropertyChanged(); } }
        private int _PositionsPerWheel = 6;

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

        /// <summary>
        /// Get the wheel number and position based on HoleIndex and positions per wheel
        /// </summary>
        /// <param name="positionsPerWheel">Number of positions per wheel</param>
        /// <returns>Display string in format "Wheel-Position" (e.g., "1-0", "2-5")</returns>
        public string GetPositionDisplay(int positionsPerWheel = 6)
        {
            int wheelNumber = (HoleIndex / positionsPerWheel) + 1;
            return $"{wheelNumber}-{HoleIndex}";
        }

        public HoleMap Clone() => new() { HoleIndex = HoleIndex, HoleName = HoleName };
    }
}
using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ColorVision.UI
{
    public class DisPlayGroupConfig : ViewModelBase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); } }

        [Newtonsoft.Json.JsonIgnore, System.Text.Json.Serialization.JsonIgnore, Browsable(false)]
        public string DisplayName => DisPlayManager.GetGroupDisplayName(this);
        private string _Name = string.Empty;

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; OnPropertyChanged(); } }
        private bool _IsExpanded = true;
    }

    public class DisPlayManagerConfig : ViewModelBase,IConfig
    {
        public const string DefaultGroupId = "__default__";
        public static DisPlayManagerConfig Instance => ConfigService.Instance.GetRequiredService<DisPlayManagerConfig>();

        public Dictionary<string, int> StoreIndex { get; set; } = new Dictionary<string, int>();
        public HashSet<string> PinnedControls { get; set; } = new();
        public HashSet<string> HiddenControls { get; set; } = new();
        public Dictionary<string, string> ControlGroups { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, bool> ControlExpandedStates { get; set; } = new Dictionary<string, bool>();
        public ObservableCollection<DisPlayGroupConfig> Groups { get; set; } = new ObservableCollection<DisPlayGroupConfig>();

        public int LastSelectIndex { get => _LastSelectIndex; set { _LastSelectIndex = value; OnPropertyChanged(); } }
        private int _LastSelectIndex ;

        public string LastSelectedControlKey { get; set; } = string.Empty;
    }
}

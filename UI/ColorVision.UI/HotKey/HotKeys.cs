using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.HotKey
{
    [Serializable]
    public class HotKeys : INotifyPropertyChanged
    {
        [Obsolete("Use HotkeyService.GetInstance().SetDefault() instead.")]
        public static void SetDefault()
        {
            HotkeyService.GetInstance().SetDefault();
        }

        public HotKeys()
        {
        }

        /// <summary>
        /// 这种方式初始化会保留初始参数
        /// </summary>
        public HotKeys(string name, Hotkey hotkey , HotKeyCallBackHanlder hotKeyCallBackHanlder)
        {
            Name = name;
            Hotkey = new Hotkey(hotkey.Key, hotkey.Modifiers);
            DefaultHotkey = new Hotkey(hotkey.Key, hotkey.Modifiers);
            HotKeyHandler += hotKeyCallBackHanlder;
        }
        [JsonIgnore]
        public Control? Control { get; set; }

        public string Id { get => _Id; set { if (value == _Id) return; _Id = value; NotifyPropertyChanged(); } }
        private string _Id = string.Empty;

        public string Name { get => _Name; set { if (value == _Name) return; _Name = value; NotifyPropertyChanged(); } }
        private string _Name = string.Empty;

        // Presentation is discovered from providers, never persisted as a shortcut override.
        // Name remains unchanged because old saved settings still match against it.
        [JsonIgnore]
        public string DisplayName { get => _displayName; set { if (value == _displayName) return; _displayName = value ?? string.Empty; NotifyPropertyChanged(); } }
        private string _displayName = string.Empty;

        [JsonIgnore]
        public string Description { get => _description; set { if (value == _description) return; _description = value ?? string.Empty; NotifyPropertyChanged(); } }
        private string _description = string.Empty;

        [JsonIgnore]
        public string Category { get => _category; set { if (value == _category) return; _category = value ?? string.Empty; NotifyPropertyChanged(); } }
        private string _category = string.Empty;

        [JsonIgnore]
        public string Source { get => _source; set { if (value == _source) return; _source = value ?? string.Empty; NotifyPropertyChanged(); } }
        private string _source = string.Empty;

        [JsonIgnore]
        public HotKeyCallBackHanlder? HotKeyHandler { get; set; }

        [JsonIgnore]
        public Hotkey DefaultHotkey { get; set; } = new();

        [JsonIgnore]
        public List<Hotkey> DefaultAdditionalHotkeys
        {
            get => _defaultAdditionalHotkeys;
            set => _defaultAdditionalHotkeys = HotkeyBindingCollection.Copy(value);
        }
        private List<Hotkey> _defaultAdditionalHotkeys = new();

        [JsonIgnore]
        public HotKeyKinds DefaultKinds { get; set; } = HotKeyKinds.Windows;

        [JsonIgnore]
        internal IHotkeyRegistration? Registration { get; set; }

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Hotkey Hotkey
        {
            get => _Hotkey;  set  
            {
                if (value == _Hotkey) return; 
                _Hotkey = value ?? new Hotkey();
                NotifyPropertyChanged(); 
            }
        }
        private Hotkey _Hotkey = new Hotkey() { Key = Key.None, Modifiers = ModifierKeys.None };

        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<Hotkey> AdditionalHotkeys
        {
            get => _additionalHotkeys;
            set
            {
                _additionalHotkeys = HotkeyBindingCollection.Copy(value);
                NotifyPropertyChanged();
            }
        }
        private List<Hotkey> _additionalHotkeys = new();

        public IReadOnlyList<Hotkey> GetBindings() => HotkeyBindingCollection.Collect(Hotkey, AdditionalHotkeys);

        public void SetBindings(IEnumerable<Hotkey> bindings)
        {
            List<Hotkey> copy = HotkeyBindingCollection.Copy(bindings);
            Hotkey = copy.Count > 0 ? copy[0] : new Hotkey();
            AdditionalHotkeys = copy.Skip(1).ToList();
        }

        public IReadOnlyList<Hotkey> GetDefaultBindings() => HotkeyBindingCollection.Collect(DefaultHotkey, DefaultAdditionalHotkeys);

        public void SetDefaultBindings(IEnumerable<Hotkey> bindings)
        {
            List<Hotkey> copy = HotkeyBindingCollection.Copy(bindings);
            DefaultHotkey = copy.Count > 0 ? copy[0] : new Hotkey();
            DefaultAdditionalHotkeys = copy.Skip(1).ToList();
        }

        public HotKeyKinds Kinds
        {
            get => _Kinds; set
            {
                if (value == _Kinds) return;
                _Kinds = value;
                NotifyPropertyChanged(nameof(IsGlobal));
                NotifyPropertyChanged();
            }
        }
        public bool IsGlobal
        {
            get => Kinds == HotKeyKinds.Global; set
            {
                if (value)
                {
                    Kinds = HotKeyKinds.Global;
                }
                else
                {
                    Kinds = HotKeyKinds.Windows;
                }
            }
        }
        private HotKeyKinds _Kinds = HotKeyKinds.Windows;





        /// <summary>
        /// 不允许外部写入
        /// </summary>
        public bool IsRegistered { get => _IsRegistered; internal set { _IsRegistered = value; NotifyPropertyChanged(); } }
        private bool _IsRegistered;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

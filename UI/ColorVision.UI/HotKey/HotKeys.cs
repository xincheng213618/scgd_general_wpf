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
            Hotkey = hotkey;
            DefaultHotkey = hotkey;
            HotKeyHandler += hotKeyCallBackHanlder;
        }
        [JsonIgnore]
        public Control? Control { get; set; }

        public string Id { get => _Id; set { if (value == _Id) return; _Id = value; NotifyPropertyChanged(); } }
        private string _Id = string.Empty;

        public string Name { get => _Name; set { if (value == _Name) return; _Name = value; NotifyPropertyChanged(); } }
        private string _Name = string.Empty;
        [JsonIgnore]
        public HotKeyCallBackHanlder? HotKeyHandler { get; set; }

        [JsonIgnore]
        public Hotkey DefaultHotkey { get; set; } = Hotkey.None;

        [JsonIgnore]
        public HotKeyKinds DefaultKinds { get; set; } = HotKeyKinds.Windows;

        [JsonIgnore]
        internal IHotkeyRegistration? Registration { get; set; }

        public Hotkey Hotkey
        {
            get => _Hotkey;  set  
            {
                if (value == _Hotkey) return; 
                _Hotkey = value ?? Hotkey.None;
                NotifyPropertyChanged(); 
            }
        }
        private Hotkey _Hotkey = new Hotkey() { Key = Key.None, Modifiers = ModifierKeys.None };
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

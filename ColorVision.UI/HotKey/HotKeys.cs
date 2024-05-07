using ColorVision.UI.HotKey.GlobalHotKey;
using ColorVision.UI.HotKey.WindowHotKey;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{

    [Serializable]
    public class HotKeys : INotifyPropertyChanged
    {
        public static readonly List<HotKeys> HotKeysList = new List<HotKeys>();
        public static readonly Dictionary<HotKeys,Hotkey> HotKeysDefaultHotkey = new Dictionary<HotKeys, Hotkey>();

        /// <summary>
        /// 设置为默认方法
        /// </summary>
        public static void SetDefault()
        {
            foreach (var item in HotKeysDefaultHotkey)
            {
                item.Key.Hotkey = Hotkey.None;
            }
            foreach (var item in HotKeysDefaultHotkey)
            {
                item.Key.Hotkey = item.Value;
            }
        }

        public HotKeys()
        {
            HotKeysList.Add(this);
        }

        /// <summary>
        /// 这种方式初始化会保留初始参数
        /// </summary>
        public HotKeys(string name, Hotkey hotkey , HotKeyCallBackHanlder hotKeyCallBackHanlder)
        {
            HotKeysList.Add(this);
            HotKeysDefaultHotkey.Add(this,hotkey);
            Name = name;
            Hotkey = hotkey;
            HotKeyHandler += hotKeyCallBackHanlder;
        }
        public Control Control { get; set; }

        public string Name { get => _Name; set { if (value == _Name) return; _Name = value; NotifyPropertyChanged(); } }
        private string _Name = string.Empty;
        public HotKeyCallBackHanlder HotKeyHandler { get; set; }
        public Hotkey Hotkey
        {
            get => _Hotkey;  set  
            {
                if (value == _Hotkey) return; 
                _Hotkey = value;
                //如果已经注册，在修改方法的时候，检测是否注册成功
                if (Control!=null)
                    if (Kinds == HotKeyKinds.Global)
                    {
                        IsRegistered = GlobalHotKeyManager.GetInstance(Window.GetWindow(Control)).ModifiedHotkey(this);
                    }
                    else
                    {
                        IsRegistered = WindowHotKeyManager.GetInstance(Control).ModifiedHotkey(this);
                    }
                NotifyPropertyChanged(); 
            }
        }
        private Hotkey _Hotkey = Hotkey.None;
        public HotKeyKinds Kinds
        {
            get => _Kinds; set
            {
                if (value == _Kinds) return;
                if (_Kinds == HotKeyKinds.Global)
                {
                    GlobalHotKeyManager.GetInstance(Window.GetWindow(Control)).UnRegister(this);
                }
                else
                {
                    WindowHotKeyManager.GetInstance(Control).UnRegister(this);
                }
                _Kinds = value;
                if (_Kinds == HotKeyKinds.Global)
                {
                    IsRegistered = GlobalHotKeyManager.GetInstance(Window.GetWindow(Control)).Register(this);
                }
                else
                {
                    IsRegistered = WindowHotKeyManager.GetInstance(Control).Register(this);
                }

                NotifyPropertyChanged(nameof(IsGlobal));
                NotifyPropertyChanged();
            }
        }
        private HotKeyKinds _Kinds = HotKeyKinds.Windows;

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



        /// <summary>
        /// 不允许外部写入
        /// </summary>
        public bool IsRegistered { get => _IsRegistered; internal set { _IsRegistered = value; NotifyPropertyChanged(); } }
        private bool _IsRegistered;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

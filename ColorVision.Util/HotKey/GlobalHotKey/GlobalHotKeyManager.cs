﻿using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColorVision.HotKey.GlobalHotKey
{
    public class GlobalHotKeyManager
    {
        public IntPtr WindowHandle { get; set; } 
        private GlobalHotKeyManager(IntPtr intPtr)
        {
            this.WindowHandle = intPtr;
        }
        private static readonly object locker = new object();

        public static GlobalHotKeyManager GetInstance(Window window)
        {
            IntPtr intPtr = new WindowInteropHelper(window).EnsureHandle();
            lock (locker) { return new GlobalHotKeyManager(intPtr); }
        }


        public bool Register(HotKeys hotKeys)
        {
            if (hotKeys == null) return false;
            if (hotKeys.Kinds == HotKeyKinds.Global)
            {
                return GlobalHotKey.Register(WindowHandle, hotKeys.Hotkey.Modifiers, hotKeys.Hotkey.Key, hotKeys.HotKeyHandler);
            }
            return false;
        }

        public bool Register(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (hotkey == null) return false;
            return GlobalHotKey.Register(WindowHandle, hotkey.Modifiers, hotkey.Key, callBack);
        }
        public bool Register(ModifierKeys modifierKeys, Key key, HotKeyCallBackHanlder callBack)
        {
            return GlobalHotKey.Register(WindowHandle, modifierKeys, key, callBack);
        }

        public void UnRegister(HotKeys hotKeys)
        {
            GlobalHotKey.UnRegister(WindowHandle, hotKeys.HotKeyHandler);
        }
        public void UnRegister(HotKeyCallBackHanlder callBack)
        {
            GlobalHotKey.UnRegister(WindowHandle, callBack);
        }

        public bool ModifiedHotkey(HotKeys hotkeys)
        {
            GlobalHotKey.UnRegister(WindowHandle, hotkeys.HotKeyHandler);
            return hotkeys.Hotkey != null && hotkeys.Hotkey == Hotkey.None && GlobalHotKey.Register(WindowHandle, hotkeys.Hotkey.Modifiers, hotkeys.Hotkey.Key, hotkeys.HotKeyHandler);
        }

        public void ModifiedHotkey(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (callBack == null) return;
            GlobalHotKey.UnRegister(WindowHandle, callBack);
            if (hotkey != null) GlobalHotKey.Register(WindowHandle, hotkey.Modifiers, hotkey.Key, callBack);

        }
    }
    

}

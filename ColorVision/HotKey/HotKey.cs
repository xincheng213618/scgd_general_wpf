using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;

namespace ColorVision.HotKey
{
    public static class HotKeyExtension
    {
        public static int ToInt(this Hotkey hotkey) => Hotkey.ToInt(hotkey);
        public static bool IsNullOrEmpty(this Hotkey hotkey) => Hotkey.IsNullOrEmpty(hotkey);
    }


    [Serializable]
    public class Hotkey
    {
        
        #region static
        public readonly static Hotkey None = new Hotkey(Key.None,ModifierKeys.None);
        public static bool IsNullOrEmpty(Hotkey hotkey) => hotkey != null && hotkey != None;
        public static int ToInt(Hotkey hotkey) => ((int)hotkey.Modifiers << 8) + (int)hotkey.Key;
        #endregion
        public Key Key { get; }
        public ModifierKeys Modifiers { get; }

        public Hotkey(Key key, ModifierKeys modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public override string ToString()
        {
            if (Key == Key.None && Modifiers == ModifierKeys.None)
                return "<None>";

            var str = new StringBuilder();
            if (Modifiers.HasFlag(ModifierKeys.Windows))
                str.Append("Win + ");
            if (Modifiers.HasFlag(ModifierKeys.Control))
                str.Append("Ctrl + ");
            if (Modifiers.HasFlag(ModifierKeys.Shift))
                str.Append("Shift + ");
            if (Modifiers.HasFlag(ModifierKeys.Alt))
                str.Append("Alt + ");

            str.Append(Key);
            return str.ToString();
        }
    }
}

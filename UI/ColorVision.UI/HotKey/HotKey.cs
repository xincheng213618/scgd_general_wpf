using System.Text;
using System.Windows.Input;

namespace ColorVision.UI.HotKey
{



    public static class HotKeyExtension
    {
        public static int ToInt(this Hotkey? hotkey) => Hotkey.ToInt(hotkey);
        public static bool IsNullOrEmpty(this Hotkey? hotkey) => Hotkey.IsNullOrEmpty(hotkey);
    }


    [Serializable]
    public class Hotkey : IEquatable<Hotkey>
    {
        
        #region static
        public readonly static Hotkey None = new(Key.None,ModifierKeys.None);
        public static bool IsNullOrEmpty(Hotkey? hotkey) => hotkey == null || hotkey.IsEmpty;
        public static int ToInt(Hotkey? hotkey) => hotkey == null ? 0 : ((int)hotkey.Modifiers << 8) + (int)hotkey.Key;
        #endregion

        public Key Key { get; set; } = Key.None;
        public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;
        public bool IsEmpty => Key == Key.None && Modifiers == ModifierKeys.None;

        public Hotkey()
        {
        }

        public Hotkey(Key key, ModifierKeys modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public bool Equals(Hotkey? other)
        {
            return other != null && Key == other.Key && Modifiers == other.Modifiers;
        }

        public override bool Equals(object? obj)
        {
            return obj is Hotkey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, Modifiers);
        }

        public static bool operator ==(Hotkey? left, Hotkey? right)
        {
            return EqualityComparer<Hotkey>.Default.Equals(left, right);
        }

        public static bool operator !=(Hotkey? left, Hotkey? right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            if (IsEmpty)
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

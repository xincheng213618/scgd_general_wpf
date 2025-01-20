using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
{
    /// <summary>
    /// 键盘操作
    /// </summary>
    internal static class Keyboard
    {
        [DllImport("user32.dll", EntryPoint = "keybd_event", SetLastError = true)]
        private static extern void KeybdEvent(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", EntryPoint = "keybd_event", SetLastError = true)]
        private static extern void KeybdEvent(System.Windows.Forms.Keys bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        /// <summary>
        /// Key down flag
        /// </summary>
        private const int KEY_DOWN_EVENT = 0x0001;
        /// <summary>
        /// Key up flag
        /// </summary>
        private const int KEY_UP_EVENT = 0x0002;
        /// <summary>
        /// 按住一个键时在两次击键之间等待 50ms
        /// </summary>
        private const int PauseBetweenStrokes = 50;


        public static void HoldKey(System.Windows.Forms.Keys key, int duration) => HoldKey((byte)key,  duration);

        public static void HoldKey(byte key, int duration)
        {
            int totalDuration = 0;
            while (totalDuration < duration)
            {
                KeybdEvent(key, 0, KEY_DOWN_EVENT, 0);
                KeybdEvent(key, 0, KEY_UP_EVENT, 0);
                System.Threading.Thread.Sleep(PauseBetweenStrokes);
                totalDuration += PauseBetweenStrokes;
            }
        }


        public static void PressKey(System.Windows.Forms.Keys key) => PressKey((byte)key);
        public static void PressKey(byte key)
        {
            KeybdEvent(key, 0, KEY_DOWN_EVENT, 0);
            KeybdEvent(key, 0, KEY_UP_EVENT, 0);
        }



        public static void KeyUp(System.Windows.Forms.Keys key) => KeyUp((byte)key);
        public static void KeyUp(byte key)=> KeybdEvent(key, 0, KEY_UP_EVENT, 0);
    }
}

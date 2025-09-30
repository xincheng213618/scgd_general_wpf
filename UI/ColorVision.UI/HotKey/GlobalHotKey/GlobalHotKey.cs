using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColorVision.UI.HotKey.GlobalHotKey
{

    /// <summary>
    /// 热键管理器
    /// </summary>
    public static class GlobalHotKey
    {
        /// <summary>
        /// 热键消息
        /// </summary>
        public const int WMHOTKEY = 0x312;

        /// <summary>
        /// 注册热键
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, ModifierKeys fsModifuers, uint vk);

        /// <summary>
        /// 注销热键
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>
        /// 向原子表中添加全局原子
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern short GlobalAddAtom(string lpString);

        /// <summary>
        /// 在表中搜索全局原子
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern short GlobalFindAtom(string lpString);

        /// <summary>
        /// 在表中删除全局原子
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern short GlobalDeleteAtom(short nAtom);



        static Dictionary<int, HotKeyCallBackHanlder> keymap = new();
        static List<HwndSource> HwndHook = new();
        static int keyid;

        /// <summary>
        /// 注册快捷键
        /// </summary>
        /// <param name="window">持有快捷键窗口</param>
        /// <param name="fsModifiers">组合键</param>
        /// <param name="key">快捷键</param>
        /// <param name="callBack">回调函数</param>
        public static bool Register(IntPtr hwnd, ModifierKeys fsModifiers, Key key, HotKeyCallBackHanlder callBack)
        {
            HwndSource _hwndSource = HwndSource.FromHwnd(hwnd);
            if (!HwndHook.Contains(_hwndSource))
            {
                _hwndSource.AddHook(WndProc);
                HwndHook.Add(_hwndSource);
            }
            int id = keyid++;
            int vk = KeyInterop.VirtualKeyFromKey(key);

            if (!RegisterHotKey(hwnd, id, fsModifiers, (uint)vk))
            {
                return false;
            }
            else
            {
                keymap[id] = callBack;
                return true;
            }
        }

        /// <summary>
        /// 可以自定义id
        /// </summary>
        public static bool Register(IntPtr hwnd, int id , ModifierKeys fsModifiers, Key key, HotKeyCallBackHanlder callBack)
        {
            HwndSource _hwndSource = HwndSource.FromHwnd(hwnd);
            if (!HwndHook.Contains(_hwndSource))
            {
                _hwndSource.AddHook(WndProc);
                HwndHook.Add(_hwndSource);
            }
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (!RegisterHotKey(hwnd, id, fsModifiers, (uint)vk))
            {
                UnregisterHotKey(hwnd, id);
                return false;
            }
            else
            {
                keymap[id] = callBack;
                return true;
            }
        }

        /// <summary>
        /// 快捷键消息处理
        /// </summary>
        /// 这里用一个就可以，不管开了几个窗口，最后触发的都是绑定的事件
        static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            //https://wiki.winehq.org/List_Of_Windows_Messages
            if (msg == WMHOTKEY)
            {
                int id = wParam.ToInt32();
                if (keymap.TryGetValue(id, out var callback))
                {
                    callback();
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 注销快捷键
        /// </summary>
        /// <param name="hWnd">持有快捷键窗口的句柄</param>
        /// <param name="callBack">回调函数</param>
        public static void UnRegister(IntPtr hWnd, HotKeyCallBackHanlder callBack)
        {
            var keysToRemove = keymap.Where(kv => kv.Value == callBack).Select(kv => kv.Key).ToList();
            foreach (var key in keysToRemove)
            {
                UnregisterHotKey(hWnd, key);
                keymap.Remove(key);
            }
        }

    }
}

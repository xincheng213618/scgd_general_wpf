
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Common.NativeMethods
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)]
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    public static class ShellFileOperations
    {
        public const uint FO_COPY = 0x0002;
        public const uint FO_MOVE = 0x0001; // 添加 FO_MOVE 常量
        public const ushort FOF_NOCONFIRMATION = 0x0010;
        public const ushort FOF_NOCONFIRMMKDIR = 0x0200;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        public static int Move(string source, string dest)
        {
            SHFILEOPSTRUCT fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_MOVE,
                pFrom = source + '\0',
                pTo = dest + '\0',
                fFlags = FOF_NOCONFIRMATION | FOF_NOCONFIRMMKDIR
            };
            int result = SHFileOperation(ref fileOp);
            return result;
        }

        public static int Copy(string source, string dest)
        {
            SHFILEOPSTRUCT fileOp = new SHFILEOPSTRUCT
            {
                wFunc = FO_COPY,
                pFrom = source + '\0',
                pTo = dest + '\0',
                fFlags = FOF_NOCONFIRMATION | FOF_NOCONFIRMMKDIR
            };
            int result = SHFileOperation(ref fileOp);
            return result;
        }
    }
}

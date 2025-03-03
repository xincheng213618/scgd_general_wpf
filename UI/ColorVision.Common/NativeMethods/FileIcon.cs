#pragma warning disable CS8604
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using ColorVision.Common.Utilities;
using System.Collections.Generic;

namespace ColorVision.Common.NativeMethods
{
    public class FileIcon
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public IntPtr iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        /// <summary>
        /// 返回系统设置的图标
        /// </summary>
        /// <param name="pszPath">文件路径 如果为""  返回文件夹的</param>
        /// <param name="dwFileAttributes">0</param>
        /// <param name="psfi">结构体</param>
        /// <param name="cbSizeFileInfo">结构体大小</param>
        /// <param name="uFlags">枚举类型</param>
        /// <returns>-1失败</returns>
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        public enum SHGFI
        {
            ICON = 0x100,
            USEFILEATTRIBUTES = 0x10,
            DISPLAYNAME = 0x200,  // Gets the Display name
            TYPENAME = 0x400,     // Gets the type name
            LARGEICON = 0x0,     // Large icon
            SMALLICON = 0x1      // Small icon
        }

        /// <summary>
        /// 获取文件图标
        /// </summary>
        /// <param name="p_Path">文件全路径</param>
        /// <returns>图标</returns>
        public static Icon? GetFileIcon(string path, bool smallIcon = false)
        {
            SHFILEINFO _SHFILEINFO = new();
            uint flag = (uint)(SHGFI.ICON | SHGFI.USEFILEATTRIBUTES | (smallIcon ? SHGFI.SMALLICON : SHGFI.LARGEICON));
            IntPtr _IconIntPtr = SHGetFileInfo(path, 0, ref _SHFILEINFO, (uint)Marshal.SizeOf(_SHFILEINFO), flag);
            if (_IconIntPtr.Equals(IntPtr.Zero)) return null;
            Icon _Icon = Icon.FromHandle(_SHFILEINFO.hIcon);
            return _Icon;
        }
        public static Dictionary<string, ImageSource> FileImageSourceCache { get; set; } = new Dictionary<string, ImageSource>();

        public static ImageSource GetFileIconImageSource(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

            string extension = Path.GetExtension(path);
            if (FileImageSourceCache.TryGetValue(extension, out ImageSource source))
            {
                return source;
            }

            Icon icon = GetFileIcon(path);
            source = icon.ToImageSource();

            FileImageSourceCache.TryAdd(extension, source);
            return source;
        }


        public static ImageSource? DirectoryIconImageSource { get; set; }
        public static ImageSource? GetDirectoryIconImageSource()
        {
            if (DirectoryIconImageSource == null)
            {
                Icon icon = GetDirectoryIcon();
                DirectoryIconImageSource = icon?.ToImageSource();
            }
            return DirectoryIconImageSource;

        }
        /// <summary>
        /// 获取文件夹图标 
        /// </summary>
        /// <returns>图标</returns>
        public static Icon? GetDirectoryIcon(bool smallIcon = false)
        {
            SHFILEINFO _SHFILEINFO = new();
            uint flag = (uint)(SHGFI.ICON | (smallIcon ? SHGFI.SMALLICON : SHGFI.LARGEICON));
            IntPtr _IconIntPtr = SHGetFileInfo(@"", 0, ref _SHFILEINFO, (uint)Marshal.SizeOf(_SHFILEINFO), flag);
            if (_IconIntPtr.Equals(IntPtr.Zero)) return null;
            Icon _Icon = Icon.FromHandle(_SHFILEINFO.hIcon);
            return _Icon;
        }

        /// <summary>
        /// 获取给定文件或文件夹所关联图标所对应的 ImageSource；
        /// </summary>
        /// <param name="file">目标文件或文件夹</param>
        /// <param name="smallIcon">需要关联的图标是小图标或者大图标</param>
        /// <returns>文件所关联Icon所对应的数据源</returns>
        public static ImageSource? GetImageSource(string file, bool smallIcon)
        {
            try
            {
                Icon icon = Directory.Exists(file) ? GetDirectoryIcon(smallIcon) : GetFileIcon(file, smallIcon);
                return icon == null ? null : Imaging.CreateBitmapSourceFromHIcon(icon.Handle, new Int32Rect(0, 0, icon.Width, icon.Height), BitmapSizeOptions.FromEmptyOptions());
            }
            catch (Exception ee)
            {
                Trace.WriteLine("### [" + ee.Source + "] Exception: " + ee.Message);
                Trace.WriteLine("### " + ee.StackTrace);
                return null;
            }
        }
    }

}

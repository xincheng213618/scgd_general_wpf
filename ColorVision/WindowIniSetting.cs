using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision
{
    public class WindowIniSetting
    {
        /// 这里就是放在当前目录下的config文件夹下的config.ini文件，不管是否是理员权限都可以读取
        private static string ConfigPath = Environment.CurrentDirectory + "\\config\\config.ini";
        public static bool IsExist { get => File.Exists(ConfigPath); }
        public static ImageSource? Icon
        {
            get
            {
                string iconPath = Common.NativeMethods.IniFile.ReadStringFromIniFile(ConfigPath, nameof(WindowIniSetting), nameof(Icon), "");
                if (iconPath != null && File.Exists(iconPath))
                {
                    if (Path.GetExtension(iconPath).Contains("ico"))
                    {
                        using var stream = new MemoryStream();
                        using System.Drawing.Icon icon = new(iconPath);
                        var bitmap = icon.ToBitmap();
                        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                        stream.Seek(0, SeekOrigin.Begin);

                        var image = new BitmapImage();
                        image.BeginInit();
                        image.StreamSource = stream;
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.EndInit();
                        return image;
                    }
                    return new BitmapImage(new Uri(iconPath));
                }
                return null;
            }
        }
        public static string? Title 
        {
            get
            {
                string Title = Common.NativeMethods.IniFile.ReadStringFromIniFile(ConfigPath, nameof(WindowIniSetting), nameof(Title), string.Empty);
                return string.IsNullOrEmpty(Title) ? null : Title;
            }
        }
    }
}

using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision
{
    public class WindowConfig
    {
        private static string ConfigPath = Environment.CurrentDirectory + "\\config\\config.ini";

        public static bool IsExist { get => File.Exists(ConfigPath); }

        public static ImageSource? Icon
        {
            get
            {
                string iconPath = NativeMethods.IniFile.ReadStringFromIniFile(ConfigPath, nameof(WindowConfig), nameof(Icon), "");
                if (iconPath != null && File.Exists(iconPath))
                {
                    if (Path.GetExtension(iconPath).Contains("ico"))
                    {
                        using var stream = new MemoryStream();
                        using System.Drawing.Icon icon = new System.Drawing.Icon(iconPath);
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
                string Title = NativeMethods.IniFile.ReadStringFromIniFile(ConfigPath, nameof(WindowConfig), nameof(Title), string.Empty);
                return Title == "" ? null : Title;
            }
        }
    }
}

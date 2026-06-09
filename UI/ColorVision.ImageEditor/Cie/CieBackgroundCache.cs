#pragma warning disable CA1859
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Cie
{
    public static class CieBackgroundCache
    {
        private const string CacheVersion = "v3";
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<CieDiagramKind, BitmapSource> MemoryCache = new();

        public static BitmapSource Get(CieDiagramProfile profile)
        {
            lock (SyncRoot)
            {
                if (MemoryCache.TryGetValue(profile.Kind, out BitmapSource? cached))
                {
                    return cached;
                }

                BitmapSource bitmap = TryLoadDiskCache(profile) ?? RenderAndSave(profile);
                MemoryCache[profile.Kind] = bitmap;
                return bitmap;
            }
        }

        private static BitmapSource? TryLoadDiskCache(CieDiagramProfile profile)
        {
            string path = GetCachePath(profile);
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                BitmapImage image = new();
                image.BeginInit();
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private static BitmapSource RenderAndSave(CieDiagramProfile profile)
        {
            BitmapSource bitmap = CieBackgroundRenderer.Render(profile);

            try
            {
                Directory.CreateDirectory(GetCacheDirectory());
                using FileStream stream = new(GetCachePath(profile), FileMode.Create, FileAccess.Write, FileShare.None);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
            catch
            {
            }

            return bitmap;
        }

        private static string GetCacheDirectory()
        {
            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localApplicationData, "ColorVision", "ImageEditor", "CieCache");
        }

        private static string GetCachePath(CieDiagramProfile profile)
        {
            return Path.Combine(GetCacheDirectory(), $"{profile.Kind}_{CacheVersion}.png");
        }
    }
}

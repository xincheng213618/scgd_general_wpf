﻿using Newtonsoft.Json;
using System.Windows;

namespace ColorVision.Services
{
    internal static class Util
    {
        public static bool IsInvalidPath(string Path, string Hint = "名称")
        {
            if (string.IsNullOrEmpty(Path))
            {
                MessageBox.Show($"{Hint}不能为空", "ColorVision");
                return false;
            }
            if (string.IsNullOrWhiteSpace(Path))
            {
                MessageBox.Show($"{Hint}不能为空白", "ColorVision");
                return false;
            }
            if (Path.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show($"{Hint}不能包含特殊字符", "ColorVision");
                return false;
            }
            return true;
        }

        public static T? DeserializeObject<T>(string? str)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(str ?? string.Empty);
            }
            catch
            {
                return default;
            };
        }


    }
}

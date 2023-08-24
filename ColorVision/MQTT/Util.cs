using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.MQTT
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
    }
}

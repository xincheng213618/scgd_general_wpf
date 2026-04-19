using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

namespace CaptchaOCR
{
    /// <summary>
    /// 验证码生成器 - 生成带干扰的验证码图片
    /// </summary>
    public class CaptchaGenerator
    {
        private const string Charset = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static readonly Random Random = new();

        public int Width { get; set; } = 160;
        public int Height { get; set; } = 60;
        public int Length { get; set; } = 4;
        public int FontSize { get; set; } = 36;

        /// <summary>
        /// 生成验证码
        /// </summary>
        /// <returns>(图片, 文本)</returns>
        public (Bitmap Image, string Text) Generate()
        {
            var text = GenerateText();
            var image = DrawCaptcha(text);
            return (image, text);
        }

        private string GenerateText()
        {
            var chars = new char[Length];
            for (int i = 0; i < Length; i++)
            {
                chars[i] = Charset[Random.Next(Charset.Length)];
            }
            return new string(chars);
        }

        private Bitmap DrawCaptcha(string text)
        {
            var bitmap = new Bitmap(Width, Height);
            using var g = Graphics.FromImage(bitmap);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // 白色背景
            g.Clear(Color.White);

            // 绘制干扰线
            DrawNoiseLines(g);

            // 绘制噪点
            DrawNoisePoints(bitmap);

            // 绘制文字
            DrawText(g, text);

            // 扭曲效果
            bitmap = Distort(bitmap);

            return bitmap;
        }

        private void DrawText(Graphics g, string text)
        {
            var charWidth = Width / Length;

            // 尝试使用系统字体
            Font? font = null;
            string[] fontNames = { "Arial", "Microsoft YaHei", "SimSun", "Consolas", "Segoe UI" };
            foreach (var name in fontNames)
            {
                try
                {
                    font = new Font(name, FontSize, FontStyle.Bold);
                    break;
                }
                catch { }
            }
            font ??= new Font(FontFamily.GenericSansSerif, FontSize, FontStyle.Bold);

            for (int i = 0; i < text.Length; i++)
            {
                var color = RandomDarkColor();
                using var brush = new SolidBrush(color);

                // 随机位置偏移
                int x = i * charWidth + Random.Next(5, Math.Max(6, charWidth - FontSize - 5));
                int y = Random.Next(5, Math.Max(6, Height - FontSize - 5));

                // 随机旋转
                g.TranslateTransform(x + FontSize / 2, y + FontSize / 2);
                g.RotateTransform(Random.Next(-15, 15));
                g.DrawString(text[i].ToString(), font, brush, -FontSize / 2, -FontSize / 2);
                g.ResetTransform();
            }

            font.Dispose();
        }

        private void DrawNoiseLines(Graphics g)
        {
            int numLines = Random.Next(3, 6);
            for (int i = 0; i < numLines; i++)
            {
                var color = RandomColor(100, 200);
                using var pen = new Pen(color, 1);
                int x1 = Random.Next(0, Width / 2);
                int y1 = Random.Next(0, Height);
                int x2 = Random.Next(Width / 2, Width);
                int y2 = Random.Next(0, Height);
                g.DrawLine(pen, x1, y1, x2, y2);
            }
        }

        private void DrawNoisePoints(Bitmap bitmap)
        {
            int numPoints = (int)(Width * Height * 0.02);
            for (int i = 0; i < numPoints; i++)
            {
                int x = Random.Next(0, Width);
                int y = Random.Next(0, Height);
                bitmap.SetPixel(x, y, RandomColor(0, 150));
            }
        }

        private Bitmap Distort(Bitmap source)
        {
            var result = new Bitmap(Width, Height);
            using var g = Graphics.FromImage(result);
            g.Clear(Color.White);

            double angle = Random.NextDouble() * 6 - 3; // -3 to 3 degrees
            double radians = angle * Math.PI / 180;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);

            int centerX = Width / 2;
            int centerY = Height / 2;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    // 反向旋转
                    int srcX = (int)((x - centerX) * cos - (y - centerY) * sin + centerX);
                    int srcY = (int)((x - centerX) * sin + (y - centerY) * cos + centerY);

                    if (srcX >= 0 && srcX < Width && srcY >= 0 && srcY < Height)
                    {
                        result.SetPixel(x, y, source.GetPixel(srcX, srcY));
                    }
                }
            }

            source.Dispose();
            return result;
        }

        private static Color RandomColor(int minVal, int maxVal)
        {
            return Color.FromArgb(
                Random.Next(minVal, maxVal),
                Random.Next(minVal, maxVal),
                Random.Next(minVal, maxVal));
        }

        private static Color RandomDarkColor()
        {
            return Color.FromArgb(
                Random.Next(50, 150),
                Random.Next(50, 150),
                Random.Next(50, 150));
        }
    }
}

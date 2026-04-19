using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;

namespace CaptchaOCR
{
    public enum CharacterMode
    {
        Alphanumeric,
        DigitsOnly,
        LettersOnly
    }

    public class CaptchaGenerator
    {
        private static readonly Random Random = new();

        public int Width { get; set; } = 160;
        public int Height { get; set; } = 60;
        public int FontSize { get; set; } = 32;

        // 生成规则
        private int _length = 4;
        public int Length
        {
            get => _length;
            set => _length = Math.Clamp(value, 1, 16);
        }

        public CharacterMode Mode { get; set; } = CharacterMode.Alphanumeric;

        private int _digitCount = -1; // -1 表示自动（混合模式默认行为）
        public int DigitCount
        {
            get => _digitCount;
            set => _digitCount = value < 0 ? -1 : Math.Clamp(value, 0, Length);
        }

        private static readonly string Digits = "0123456789";
        private static readonly string LowerLetters = "abcdefghijklmnopqrstuvwxyz";
        private static readonly string UpperLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static readonly string AllLetters = LowerLetters + UpperLetters;
        private static readonly string Alphanumeric = Digits + AllLetters;

        public (Bitmap Image, string Text) Generate()
        {
            var text = GenerateText();
            var image = DrawCaptcha(text);
            return (image, text);
        }

        private string GenerateText()
        {
            int digitCount = GetEffectiveDigitCount();
            int letterCount = Length - digitCount;

            var chars = new List<char>(Length);

            for (int i = 0; i < digitCount; i++)
                chars.Add(Digits[Random.Next(Digits.Length)]);

            for (int i = 0; i < letterCount; i++)
                chars.Add(AllLetters[Random.Next(AllLetters.Length)]);

            // 随机打乱
            Shuffle(chars);

            return new string(chars.ToArray());
        }

        private int GetEffectiveDigitCount()
        {
            switch (Mode)
            {
                case CharacterMode.DigitsOnly:
                    return Length;
                case CharacterMode.LettersOnly:
                    return 0;
                case CharacterMode.Alphanumeric:
                default:
                    // 如果 DigitCount 未设置或超出范围，随机分配
                    if (_digitCount < 0 || _digitCount > Length)
                        return Random.Next(0, Length + 1);
                    return _digitCount;
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = Random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private Bitmap DrawCaptcha(string text)
        {
            int scale = 2;
            var largeBitmap = new Bitmap(Width * scale, Height * scale);

            using (var g = Graphics.FromImage(largeBitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                g.Clear(Color.White);
                DrawNoiseLines(g, Width * scale, Height * scale);
                DrawText(g, text, Width * scale, Height * scale);
                DrawNoisePoints(largeBitmap);
            }

            var finalBitmap = new Bitmap(Width, Height);
            using (var g = Graphics.FromImage(finalBitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(largeBitmap, 0, 0, Width, Height);
            }

            largeBitmap.Dispose();
            return finalBitmap;
        }

        private void DrawText(Graphics g, string text, int canvasWidth, int canvasHeight)
        {
            int charCount = text.Length;
            int charWidth = canvasWidth / charCount;

            Font? font = null;
            string[] fontNames = { "Arial", "Microsoft YaHei", "SimHei", "Consolas", "Segoe UI" };
            foreach (var name in fontNames)
            {
                try
                {
                    font = new Font(name, FontSize * 2, FontStyle.Bold, GraphicsUnit.Pixel);
                    break;
                }
                catch { }
            }
            font ??= new Font(FontFamily.GenericSansSerif, FontSize * 2, FontStyle.Bold, GraphicsUnit.Pixel);

            var originalTransform = g.Transform;

            for (int i = 0; i < text.Length; i++)
            {
                var color = GetRandomDarkColor();
                using var brush = new SolidBrush(color);

                float baseX = i * charWidth + charWidth / 2f;
                float baseY = canvasHeight / 2f;

                float offsetX = Random.Next(-10, 10);
                float offsetY = Random.Next(-8, 8);
                float angle = Random.Next(-20, 21);

                g.TranslateTransform(baseX + offsetX, baseY + offsetY);
                g.RotateTransform(angle);

                var charStr = text[i].ToString();
                var size = g.MeasureString(charStr, font);
                g.DrawString(charStr, font, brush, -size.Width / 2, -size.Height / 2);

                g.ResetTransform();
            }

            font.Dispose();
        }

        private void DrawNoiseLines(Graphics g, int width, int height)
        {
            int numLines = Random.Next(4, 8);
            for (int i = 0; i < numLines; i++)
            {
                var color = GetRandomLightColor();
                using var pen = new Pen(color, 2);

                int x1 = Random.Next(0, width / 3);
                int y1 = Random.Next(0, height);
                int cx1 = Random.Next(width / 4, width / 2);
                int cy1 = Random.Next(0, height);
                int cx2 = Random.Next(width / 2, width * 3 / 4);
                int cy2 = Random.Next(0, height);
                int x2 = Random.Next(width * 2 / 3, width);
                int y2 = Random.Next(0, height);

                g.DrawBezier(pen, x1, y1, cx1, cy1, cx2, cy2, x2, y2);
            }
        }

        private void DrawNoisePoints(Bitmap bitmap)
        {
            int numPoints = (int)(bitmap.Width * bitmap.Height * 0.005);
            for (int i = 0; i < numPoints; i++)
            {
                int x = Random.Next(0, bitmap.Width);
                int y = Random.Next(0, bitmap.Height);
                bitmap.SetPixel(x, y, GetRandomLightColor());
            }
        }

        private Color GetRandomLightColor()
        {
            int r = Random.Next(150, 220);
            int g = Random.Next(150, 220);
            int b = Random.Next(150, 220);
            return Color.FromArgb(r, g, b);
        }

        private Color GetRandomDarkColor()
        {
            int r = Random.Next(30, 140);
            int g = Random.Next(30, 140);
            int b = Random.Next(30, 140);
            return Color.FromArgb(r, g, b);
        }
    }
}

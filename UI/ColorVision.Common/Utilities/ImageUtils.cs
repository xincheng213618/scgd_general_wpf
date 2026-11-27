using System;
using System.Buffers;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.IO;
using System.Windows.Interop;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ColorVision.Common.Utilities
{

    public static partial class ImageUtils
    {
        private const int HistogramBinCount = 256;

        public static void DrawHistogram(int[] histogram, Color color, DrawingContext drawingContext, double width, double height)
        {
            double max = histogram.Max();
            double scale = height / max;

            Pen pen = new Pen(new SolidColorBrush(color), 1);

            for (int i = 0; i < histogram.Length; i++)
            {
                double x = i * (width / 256);
                double y = height - (histogram[i] * scale);
                drawingContext.DrawLine(pen, new Point(x, height), new Point(x, y));
            }
        }

        public static (int[] c1, int[] c2, int[] c3) RenderHistogram(BitmapSource bitmapSource)
        {
            int[] channel1 = new int[HistogramBinCount];
            int[] channel2 = new int[HistogramBinCount];
            int[] channel3 = new int[HistogramBinCount];
            PixelFormat format = bitmapSource.Format;
            if (bitmapSource.Palette != null)
            {
                bitmapSource = new FormatConvertedBitmap(bitmapSource, PixelFormats.Bgr24, null, 0);
                format = bitmapSource.Format;
            }
            bool isGray = format == PixelFormats.Gray8 || format == PixelFormats.Gray16;
            bool isWideRgb = format == PixelFormats.Rgb48 || format == PixelFormats.Rgba64;
            bool isRgb24 = format == PixelFormats.Rgb24;
            if (!isGray && !isWideRgb && !isRgb24 && format != PixelFormats.Bgr24 && format != PixelFormats.Bgra32 && format != PixelFormats.Bgr32)
                return (channel1, channel2, channel3);

            int width = bitmapSource.PixelWidth;
            int height = bitmapSource.PixelHeight;
            int pixelBytes = format.BitsPerPixel / 8;
            int stride = checked(width * pixelBytes);
            bool isBgr = !isGray && !isRgb24 && !isWideRgb;
            int[] firstColorBins = isBgr ? channel3 : channel1;
            int[] lastColorBins = isBgr ? channel1 : channel3;
            const int bandBufferBytes = 512 * 1024;
            int workers = Math.Min(Environment.ProcessorCount, 4);
            if (workers > 1 && (long)width * height >= 2 * 1024 * 1024 && stride <= bandBufferBytes && height >= 8)
            {
                CountHistogramBands(bitmapSource, width, height, stride, pixelBytes, isGray, isWideRgb,
                    firstColorBins, channel2, lastColorBins, bandBufferBytes, workers);
            }
            else
            {
                CountHistogramSerial(bitmapSource, width, height, stride, pixelBytes, isGray, isWideRgb,
                    firstColorBins, channel2, lastColorBins);
            }

            if (format == PixelFormats.Gray8)
            {
                channel1.CopyTo(channel2, 0);
                channel1.CopyTo(channel3, 0);
            }
            return (channel1, channel2, channel3);
        }

        private static void CountHistogramSerial(BitmapSource bitmapSource, int width, int height, int stride, int pixelBytes,
            bool isGray, bool isWideRgb, int[] first, int[] green, int[] last)
        {
            const int targetBufferBytes = 64 * 1024;
            int rowsPerChunk = Math.Min(height, Math.Max(1, targetBufferBytes / stride));
            int chunkBytes = checked(rowsPerChunk * stride);
            // Independent counters let repeated values accumulate without serializing every update.
            Span<int> scratch = chunkBytes >= (isGray ? 4 : 2) * pixelBytes
                ? stackalloc int[HistogramBinCount * 3] : Span<int>.Empty;
            scratch.Clear();
            byte[] pixelData = ArrayPool<byte>.Shared.Rent(chunkBytes);
            try
            {
                for (int top = 0; top < height;)
                {
                    int rowCount = Math.Min(rowsPerChunk, height - top);
                    bitmapSource.CopyPixels(new Int32Rect(0, top, width, rowCount), pixelData, stride, 0);
                    int length = rowCount * stride;
                    CountHistogramPixels(pixelData.AsSpan(0, length), pixelBytes, isGray, isWideRgb, first, green, last, scratch);
                    top += rowCount;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pixelData);
            }

            MergeHistogramScratch(first, green, last, scratch, isGray);
        }

        private sealed class HistogramBand(int bufferBytes)
        {
            internal readonly int[] First = new int[HistogramBinCount];
            internal readonly int[] Green = new int[HistogramBinCount];
            internal readonly int[] Last = new int[HistogramBinCount];
            internal readonly int[] Scratch = new int[3 * HistogramBinCount];
            internal readonly byte[] Pixels = ArrayPool<byte>.Shared.Rent(bufferBytes);
            internal int Length;
        }

        private static void CountHistogramBands(BitmapSource source, int width, int height, int stride, int pixelBytes,
            bool isGray, bool isWideRgb, int[] first, int[] green, int[] last, int bandBufferBytes, int workers)
        {
            int rowsPerBand = bandBufferBytes / stride;
            var bands = new HistogramBand[Math.Min(workers, (height - 1) / rowsPerBand + 1)];
            var options = new ParallelOptions { MaxDegreeOfParallelism = bands.Length, TaskScheduler = TaskScheduler.Default };
            try
            {
                for (int index = 0; index < bands.Length; index++) bands[index] = new HistogramBand(rowsPerBand * stride);

                for (int top = 0; top < height;)
                {
                    // WPF reads stay on the caller's thread. Workers only see the copied strips.
                    int bandCount = 0;
                    while (bandCount < bands.Length && top < height)
                    {
                        HistogramBand band = bands[bandCount++];
                        int rows = Math.Min(rowsPerBand, height - top);
                        source.CopyPixels(new Int32Rect(0, top, width, rows), band.Pixels, stride, 0);
                        band.Length = rows * stride;
                        top += rows;
                    }
                    if (bandCount == 1) CountBand(0);
                    else Parallel.For(0, bandCount, options, CountBand);
                }

                foreach (HistogramBand band in bands)
                {
                    MergeHistogramScratch(band.First, band.Green, band.Last, band.Scratch, isGray);
                    for (int i = 0; i < HistogramBinCount; i++)
                    {
                        first[i] += band.First[i];
                        green[i] += band.Green[i];
                        last[i] += band.Last[i];
                    }
                }
            }
            finally
            {
                foreach (HistogramBand? band in bands)
                    if (band != null) ArrayPool<byte>.Shared.Return(band.Pixels);
            }

            void CountBand(int index)
            {
                HistogramBand band = bands[index];
                CountHistogramPixels(band.Pixels.AsSpan(0, band.Length), pixelBytes, isGray, isWideRgb,
                    band.First, band.Green, band.Last, band.Scratch);
            }
        }

        private static void CountHistogramPixels(ReadOnlySpan<byte> pixels, int pixelBytes, bool isGray, bool isWideRgb,
            int[] first, int[] green, int[] last, Span<int> scratch)
        {
            if (isGray) CountGrayscale(pixels, pixelBytes, first, scratch);
            else if (isWideRgb) CountWideRgb(pixels, pixelBytes, first, green, last, scratch);
            else CountRgb8(pixels, pixelBytes, first, green, last, scratch);
        }

        private static void MergeHistogramScratch(int[] first, int[] green, int[] last, ReadOnlySpan<int> scratch, bool isGray)
        {
            if (scratch.IsEmpty) return;
            if (isGray)
            {
                for (int i = 0; i < HistogramBinCount; i++)
                    first[i] += scratch[i] + scratch[HistogramBinCount + i] + scratch[2 * HistogramBinCount + i];
                return;
            }
            for (int i = 0; i < HistogramBinCount; i++)
            {
                first[i] += scratch[i];
                green[i] += scratch[HistogramBinCount + i];
                last[i] += scratch[2 * HistogramBinCount + i];
            }
        }

        private static void CountRgb8(ReadOnlySpan<byte> pixels, int pixelBytes, int[] first, int[] green, int[] last, Span<int> scratch)
        {
            ref byte data = ref MemoryMarshal.GetReference(pixels);
            ref int firstBase = ref MemoryMarshal.GetArrayDataReference(first);
            ref int greenBase = ref MemoryMarshal.GetArrayDataReference(green);
            ref int lastBase = ref MemoryMarshal.GetArrayDataReference(last);
            int offset = 0;
            int pairBytes = pixelBytes * 2;
            if (pixels.Length >= pairBytes)
            {
                // Alternate bins to keep updates to repeated colors independent.
                ref int firstScratch = ref MemoryMarshal.GetReference(scratch);
                ref int greenScratch = ref Unsafe.Add(ref firstScratch, HistogramBinCount);
                ref int lastScratch = ref Unsafe.Add(ref firstScratch, 2 * HistogramBinCount);
                if (pixelBytes == 4)
                {
                    for (; offset <= pixels.Length - pairBytes; offset += pairBytes)
                    {
                        ulong pair = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref data, offset));
                        CountRgb8Pixel((uint)pair, ref firstBase, ref greenBase, ref lastBase);
                        CountRgb8Pixel((uint)(pair >> 32), ref firstScratch, ref greenScratch, ref lastScratch);
                    }
                }
                else
                {
                    for (; offset <= pixels.Length - pairBytes; offset += pairBytes)
                    {
                        CountRgb8Pixel(ref Unsafe.Add(ref data, offset), ref firstBase, ref greenBase, ref lastBase);
                        CountRgb8Pixel(ref Unsafe.Add(ref data, offset + 3), ref firstScratch, ref greenScratch, ref lastScratch);
                    }
                }
            }
            if (offset < pixels.Length)
                CountRgb8Pixel(ref Unsafe.Add(ref data, offset), ref firstBase, ref greenBase, ref lastBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CountRgb8Pixel(ref byte pixel, ref int first, ref int green, ref int last)
        {
            Unsafe.Add(ref first, pixel)++;
            Unsafe.Add(ref green, Unsafe.Add(ref pixel, 1))++;
            Unsafe.Add(ref last, Unsafe.Add(ref pixel, 2))++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CountRgb8Pixel(uint pixel, ref int first, ref int green, ref int last)
        {
            Unsafe.Add(ref first, (byte)pixel)++;
            Unsafe.Add(ref green, (byte)(pixel >> 8))++;
            Unsafe.Add(ref last, (byte)(pixel >> 16))++;
        }

        private static void CountWideRgb(ReadOnlySpan<byte> pixels, int pixelBytes, int[] red, int[] green, int[] blue, Span<int> scratch)
        {
            ref byte firstPixel = ref MemoryMarshal.GetReference(pixels);
            ref int redBase = ref MemoryMarshal.GetArrayDataReference(red);
            ref int greenBase = ref MemoryMarshal.GetArrayDataReference(green);
            ref int blueBase = ref MemoryMarshal.GetArrayDataReference(blue);
            ref int secondBins = ref MemoryMarshal.GetReference(scratch);
            // Alternating bins let identical high bytes from adjacent pixels accumulate independently.
            int pairBytes = pixelBytes * 2;
            int offset = 0;
            for (; offset <= pixels.Length - pairBytes; offset += pairBytes)
            {
                CountWideRgbPixel(ref Unsafe.Add(ref firstPixel, offset), ref redBase, ref greenBase, ref blueBase);
                CountWideRgbPixel(ref Unsafe.Add(ref firstPixel, offset + pixelBytes),
                    ref secondBins, ref Unsafe.Add(ref secondBins, HistogramBinCount), ref Unsafe.Add(ref secondBins, 2 * HistogramBinCount));
            }
            if (offset < pixels.Length)
                CountWideRgbPixel(ref Unsafe.Add(ref firstPixel, offset), ref redBase, ref greenBase, ref blueBase);
        }

        private static void CountWideRgbPixel(ref byte pixel, ref int red, ref int green, ref int blue)
        {
            // The packed pixel has at least six bytes. Only the high byte of each 16-bit channel is used.
            Unsafe.Add(ref red, Unsafe.Add(ref pixel, 1))++;
            Unsafe.Add(ref green, Unsafe.Add(ref pixel, 3))++;
            Unsafe.Add(ref blue, Unsafe.Add(ref pixel, 5))++;
        }

        private static void CountGrayscale(ReadOnlySpan<byte> pixels, int pixelBytes, int[] bins, Span<int> scratch)
        {
            if (pixelBytes == 1)
            {
                foreach (uint packed in MemoryMarshal.Cast<byte, uint>(pixels))
                {
                    bins[(byte)packed]++;
                    scratch[(byte)(packed >> 8)]++;
                    scratch[HistogramBinCount + (byte)(packed >> 16)]++;
                    scratch[2 * HistogramBinCount + (byte)(packed >> 24)]++;
                }
            }
            else
            {
                // The high byte of each little-endian Gray16 sample determines its bin.
                foreach (ulong packed in MemoryMarshal.Cast<byte, ulong>(pixels))
                {
                    bins[(byte)(packed >> 8)]++;
                    scratch[(byte)(packed >> 24)]++;
                    scratch[HistogramBinCount + (byte)(packed >> 40)]++;
                    scratch[2 * HistogramBinCount + (byte)(packed >> 56)]++;
                }
            }

            int groupBytes = pixelBytes * 4;
            for (int i = pixels.Length / groupBytes * groupBytes + pixelBytes - 1; i < pixels.Length; i += pixelBytes)
                bins[pixels[i]]++;
        }






        /// <summary>
        /// 对图标的扩展
        /// </summary>
        /// <param name="icon"></param>
        /// <returns></returns>
        public static ImageSource ToImageSource(this System.Drawing.Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }

        public static System.Drawing.Icon ToIcon(this ImageSource imageSource)
        {
            ArgumentNullException.ThrowIfNull(imageSource);
            var bitmapSource = imageSource as BitmapSource ?? throw new ArgumentException("ImageSource must be of type BitmapSource", nameof(imageSource));
            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);

                using (var bitmap = new System.Drawing.Bitmap(memoryStream))
                {
                    IntPtr hIcon = bitmap.GetHicon();
                    return System.Drawing.Icon.FromHandle(hIcon);
                }
            }
        }
        public static BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);

            var bitmapData = bitmap.LockBits(
                rect,
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width,
                bitmapData.Height,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                PixelFormats.Bgr24,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }

        public static BitmapImage CreateBitmapImage(byte[] imageData)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(imageData))
            {
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Freeze the BitmapImage to make it cross-thread accessible
            }
            return bitmapImage;
        }

        public static WriteableBitmap ToWriteableBitmap(System.Drawing.Bitmap src)
        {
            var wb = CreateCompatibleWriteableBitmap(src);
            System.Drawing.Imaging.PixelFormat format = src.PixelFormat;
            if (wb == null)
            {
                wb = new WriteableBitmap(src.Width, src.Height, 0, 0, PixelFormats.Bgra32, null);
                format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            }
            BitmapCopyToWriteableBitmap(src, wb, new System.Drawing.Rectangle(0, 0, src.Width, src.Height), 0, 0, format);
            return wb;
        }
    }



    /// <summary>
    /// 图像操作的一些静态方法
    /// </summary>
    public static partial class ImageUtils
    {

        private static BrushConverter brushConverter = new();

        public static Brush? ConvertFromString(string colorCode)
        {
            return (Brush)brushConverter.ConvertFromString(colorCode);
        }


        public static int ToInt32(this double num) => Convert.ToInt32(num);


        public static Color GetPixelColor(this BitmapSource bitmapImage, int x, int y)
        {
            if (x < 0 || x >= bitmapImage.PixelWidth || y < 0 || y >= bitmapImage.PixelHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(bitmapImage));
            }

            PixelFormat format = bitmapImage.Format;
            int bitsPerPixel = format.BitsPerPixel;
            int bytesPerPixel = (bitsPerPixel + 7) / 8; // Round up to the nearest whole byte

            byte[] pixelData = new byte[bytesPerPixel];
            CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(x, y, 1, 1));
            croppedBitmap.CopyPixels(pixelData, bytesPerPixel, 0);

            // Handle different pixel formats
            switch (format)
            {
                case PixelFormat fmt when fmt == PixelFormats.Rgb48:
                    // For RGB48 format, each channel is 16 bits
                    ushort red16 = BitConverter.ToUInt16(pixelData, 0);
                    ushort green16 = BitConverter.ToUInt16(pixelData, 2);
                    ushort blue16 = BitConverter.ToUInt16(pixelData, 4);

                    // Scale down to 8 bits
                    byte red = (byte)(red16 >> 8);
                    byte green = (byte)(green16 >> 8);
                    byte blue = (byte)(blue16 >> 8);

                    // Return color with full opacity
                    return Color.FromArgb(255, red, green, blue);

                case PixelFormat fmt when fmt == PixelFormats.Bgr24:
                    // For BGR24 format, each channel is 8 bits
                    return Color.FromArgb(255, pixelData[2], pixelData[1], pixelData[0]);

                case PixelFormat fmt when fmt == PixelFormats.Bgra32 || fmt == PixelFormats.Pbgra32:
                    // For BGRA32 and PBGRA32 formats, each channel is 8 bits
                    return Color.FromArgb(pixelData[3], pixelData[2], pixelData[1], pixelData[0]);
                case PixelFormat fmt when fmt == PixelFormats.Bgr32 || fmt == PixelFormats.Bgr32:
                    // For BGRA32 and PBGRA32 formats, each channel is 8 bits
                    return Color.FromArgb(255, pixelData[2], pixelData[1], pixelData[0]);

                case PixelFormat fmt when fmt == PixelFormats.Gray8:
                    // For Gray8 format, single channel is 8 bits
                    return Color.FromArgb(255, pixelData[0], pixelData[0], pixelData[0]);

                case PixelFormat fmt when fmt == PixelFormats.Gray16:
                    // For Gray16 format, single channel is 16 bits
                    ushort gray16 = BitConverter.ToUInt16(pixelData, 0);
                    byte gray = (byte)(gray16 >> 8);
                    return Color.FromArgb(255, gray, gray, gray);
                case PixelFormat fmt when fmt == PixelFormats.Gray32Float:
                    // For Gray16 format, single channel is 16 bits
                    float gray32Float = BitConverter.ToSingle(pixelData, 0);
                    byte gray1 = (byte)(Math.Clamp(gray32Float * 255.0f, 0, 255));
                    return Color.FromArgb(255, gray1, gray1, gray1);
                default:
                    throw new NotSupportedException($"Pixel format {format} is not supported.");
            }
        }

        public static string ToHex(this Color color) => "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

        public static Color GetPixelColor(System.Drawing.Bitmap bitmap, int x, int y) => bitmap.GetPixel(x, y).ToMediaColor();

        public static Color ToMediaColor(this System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);


        public static bool SaveImageSourceToFile(this ImageSource imageSource, string filePath)
        {
            ArgumentNullException.ThrowIfNull(imageSource);

            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            BitmapSource bitmapSource = null;

            if (imageSource is BitmapSource source)
            {
                bitmapSource = source;
            }
            else
            {
                // Attempt to convert ImageSource to BitmapSource
                try
                {
                    var drawingVisual = new DrawingVisual();
                    using (var drawingContext = drawingVisual.RenderOpen())
                    {
                        drawingContext.DrawImage(imageSource, new Rect(0, 0, imageSource.Width, imageSource.Height));
                    }

                    var renderTargetBitmap = new RenderTargetBitmap(
                        (int)imageSource.Width,
                        (int)imageSource.Height,
                        96, 96, PixelFormats.Pbgra32);
                    renderTargetBitmap.Render(drawingVisual);
                    bitmapSource = renderTargetBitmap;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while converting ImageSource to BitmapSource: {ex.Message}");
                    return false;
                }
            }

            try
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                encoder.Save(fileStream);
                return true;
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as necessary
                Console.WriteLine($"An error occurred while saving the image: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// Creates a frozen solid-color drawing with the requested logical size without allocating a pixel buffer.
        /// </summary>
        public static DrawingImage CreateSolidColorDrawing(int width, int height, Color color)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

            SolidColorBrush brush = new(color);
            brush.Freeze();

            RectangleGeometry geometry = new(new Rect(0, 0, width, height));
            geometry.Freeze();

            GeometryDrawing drawing = new(brush, null, geometry);
            drawing.Freeze();

            DrawingImage image = new(drawing);
            image.Freeze();
            return image;
        }

        public static bool TryGetImageSize(ImageSource? source, out int width, out int height)
        {
            if (source is BitmapSource bitmap)
            {
                width = bitmap.PixelWidth;
                height = bitmap.PixelHeight;
                return width > 0 && height > 0;
            }

            if (source != null &&
                double.IsFinite(source.Width) && source.Width > 0 && source.Width <= int.MaxValue &&
                double.IsFinite(source.Height) && source.Height > 0 && source.Height <= int.MaxValue)
            {
                width = (int)Math.Round(source.Width);
                height = (int)Math.Round(source.Height);
                return width > 0 && height > 0;
            }

            width = 0;
            height = 0;
            return false;
        }

        private static PixelFormat CoverFormat(System.Drawing.Bitmap src)
        {
            return src.PixelFormat switch
            {
                System.Drawing.Imaging.PixelFormat.DontCare => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Max => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Indexed => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Gdi => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format16bppRgb555 => PixelFormats.Bgr555,
                System.Drawing.Imaging.PixelFormat.Format16bppRgb565 => PixelFormats.Bgr565,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb => PixelFormats.Bgr24,
                System.Drawing.Imaging.PixelFormat.Format32bppRgb => PixelFormats.Bgr32,
                System.Drawing.Imaging.PixelFormat.Format1bppIndexed => PixelFormats.Indexed1,
                System.Drawing.Imaging.PixelFormat.Format4bppIndexed => PixelFormats.Indexed4,
                System.Drawing.Imaging.PixelFormat.Format8bppIndexed => PixelFormats.Indexed8,
                System.Drawing.Imaging.PixelFormat.Alpha => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format16bppArgb1555 => PixelFormats.Bgr101010,
                System.Drawing.Imaging.PixelFormat.PAlpha => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb => PixelFormats.Pbgra32,
                System.Drawing.Imaging.PixelFormat.Extended => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format16bppGrayScale => PixelFormats.Gray16,
                System.Drawing.Imaging.PixelFormat.Format48bppRgb => PixelFormats.Rgb48,
                System.Drawing.Imaging.PixelFormat.Format64bppPArgb => PixelFormats.Prgba64,
                System.Drawing.Imaging.PixelFormat.Canonical => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format32bppArgb => PixelFormats.Bgra32,
                System.Drawing.Imaging.PixelFormat.Format64bppArgb => PixelFormats.Rgba64,
                _ => PixelFormats.Default,
            }; ;
        }

        //创建尺寸和格式与Bitmap兼容的WriteableBitmap
        public static WriteableBitmap CreateCompatibleWriteableBitmap(System.Drawing.Bitmap src)
        {
            return new WriteableBitmap(src.Width, src.Height, 0, 0, CoverFormat(src), null);
        }
        //将Bitmap数据写入WriteableBitmap中
        public static void BitmapCopyToWriteableBitmap(System.Drawing.Bitmap src, WriteableBitmap dst, System.Drawing.Rectangle srcRect, int destinationX, int destinationY, System.Drawing.Imaging.PixelFormat srcPixelFormat)
        {
            var data = src.LockBits(new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), src.Size), System.Drawing.Imaging.ImageLockMode.ReadOnly, srcPixelFormat);
            dst.WritePixels(new Int32Rect(srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height), data.Scan0, data.Height * data.Stride, data.Stride, destinationX, destinationY);
            src.UnlockBits(data);
        }



    }
}

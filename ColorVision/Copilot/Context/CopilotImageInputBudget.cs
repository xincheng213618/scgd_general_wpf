using SkiaSharp;
using System;
using System.Threading;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotPreparedImageInput(
        byte[] Bytes,
        string MediaType,
        int SourceWidth,
        int SourceHeight,
        int PreparedWidth,
        int PreparedHeight);

    internal static class CopilotImageInputBudget
    {
        public const int MaximumDimension = 6_000;
        public const int MaximumPatches = 10_000;
        public const int PatchDimension = 32;
        private const long MaximumWorkingPixels = 40_000_000;

        public static CopilotPreparedImageInput Prepare(
            byte[] bytes,
            string mediaType,
            string label,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length == 0)
                throw new InvalidOperationException($"图片“{label}”为空文件。");

            cancellationToken.ThrowIfCancellationRequested();
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
                throw new InvalidOperationException($"图片“{label}”无法解码或尺寸无效。");

            var sourceWidth = codec.Info.Width;
            var sourceHeight = codec.Info.Height;
            var (preparedWidth, preparedHeight) = ResolvePreparedDimensions(sourceWidth, sourceHeight);
            if (sourceWidth == preparedWidth && sourceHeight == preparedHeight)
            {
                return new CopilotPreparedImageInput(
                    bytes,
                    mediaType,
                    sourceWidth,
                    sourceHeight,
                    preparedWidth,
                    preparedHeight);
            }

            var desiredScale = Math.Min(
                preparedWidth / (double)sourceWidth,
                preparedHeight / (double)sourceHeight);
            var codecSize = codec.GetScaledDimensions((float)desiredScale);
            if (codecSize.Width <= 0 || codecSize.Height <= 0
                || codecSize.Width > sourceWidth || codecSize.Height > sourceHeight)
            {
                codecSize = new SKSizeI(sourceWidth, sourceHeight);
            }
            if ((long)codecSize.Width * codecSize.Height > MaximumWorkingPixels)
            {
                throw new InvalidOperationException(
                    $"图片“{label}”的解码尺寸 {sourceWidth}×{sourceHeight} 超过安全处理限制，请先缩小图片。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var decodeInfo = new SKImageInfo(
                codecSize.Width,
                codecSize.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using var decoded = SKBitmap.Decode(codec, decodeInfo);
            if (decoded == null)
                throw new InvalidOperationException($"图片“{label}”无法在安全尺寸内解码，请先转换或缩小图片。");

            cancellationToken.ThrowIfCancellationRequested();
            using var resized = decoded.Resize(
                new SKImageInfo(preparedWidth, preparedHeight, SKColorType.Bgra8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell));
            if (resized == null)
                throw new InvalidOperationException($"图片“{label}”无法缩放到安全输入尺寸。");

            var (encodedFormat, preparedMediaType, quality) = ResolveOutputEncoding(mediaType);
            using var encoded = resized.Encode(encodedFormat, quality);
            if (encoded == null || encoded.Size == 0)
                throw new InvalidOperationException($"图片“{label}”缩放后无法编码。");

            var preparedBytes = encoded.ToArray();
            if (preparedBytes.LongLength > CopilotImagePayloadLoader.MaximumImageBytes)
            {
                throw new InvalidOperationException(
                    $"图片“{label}”缩放后仍超过 {CopilotImagePayloadLoader.MaximumImageBytes / 1024 / 1024} MB 限制。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new CopilotPreparedImageInput(
                preparedBytes,
                preparedMediaType,
                sourceWidth,
                sourceHeight,
                preparedWidth,
                preparedHeight);
        }

        internal static (int Width, int Height) ResolvePreparedDimensions(int width, int height)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

            var scale = Math.Min(1d, MaximumDimension / (double)Math.Max(width, height));
            var maximumPixelArea = (double)MaximumPatches * PatchDimension * PatchDimension;
            var sourcePixelArea = (double)width * height;
            scale = Math.Min(scale, Math.Sqrt(maximumPixelArea / sourcePixelArea));
            if (scale >= 1d && CountPatches(width, height) <= MaximumPatches)
                return (width, height);

            var preparedWidth = Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero));
            var preparedHeight = Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero));
            while (Math.Max(preparedWidth, preparedHeight) > MaximumDimension
                || CountPatches(preparedWidth, preparedHeight) > MaximumPatches)
            {
                if (preparedWidth >= preparedHeight)
                {
                    preparedWidth--;
                    preparedHeight = Math.Max(1, (int)Math.Round(
                        preparedWidth * height / (double)width,
                        MidpointRounding.AwayFromZero));
                }
                else
                {
                    preparedHeight--;
                    preparedWidth = Math.Max(1, (int)Math.Round(
                        preparedHeight * width / (double)height,
                        MidpointRounding.AwayFromZero));
                }
            }
            return (preparedWidth, preparedHeight);
        }

        internal static int CountPatches(int width, int height)
        {
            var horizontal = (width + PatchDimension - 1L) / PatchDimension;
            var vertical = (height + PatchDimension - 1L) / PatchDimension;
            return (int)Math.Min(int.MaxValue, horizontal * vertical);
        }

        private static (SKEncodedImageFormat Format, string MediaType, int Quality) ResolveOutputEncoding(
            string mediaType) => mediaType.ToLowerInvariant() switch
        {
            "image/jpeg" => (SKEncodedImageFormat.Jpeg, "image/jpeg", 90),
            "image/webp" => (SKEncodedImageFormat.Webp, "image/webp", 90),
            _ => (SKEncodedImageFormat.Png, "image/png", 100),
        };
    }
}

using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CameraTest.Application;

public enum FrameSourceKind { ImageFile, Capture, Live }

public sealed class TestFrame
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Source { get; }
    public StandaloneCameraFrame Data { get; }
    public FrameSourceKind SourceKind { get; }
    public StandaloneCameraOptions? AcquisitionSettings { get; }
    public TestFrame(StandaloneCameraFrame data, string source, FrameSourceKind sourceKind = FrameSourceKind.ImageFile, StandaloneCameraOptions? acquisitionSettings = null)
    {
        if (data.Pixels.Length != StandaloneCameraFrame.RequiredBytes(data.Width, data.Height, data.BitDepth, data.Channels, data.Stride))
            throw new ArgumentException("帧缓冲长度与图像格式不匹配。");
        Data = data;
        Source = source;
        SourceKind = sourceKind;
        AcquisitionSettings = sourceKind == FrameSourceKind.ImageFile ? null : acquisitionSettings?.Copy();
    }

    public static TestFrame Open(string path)
    {
        using var decoded = Cv2.ImRead(path, ImreadModes.Unchanged);
        if (decoded.Empty()) throw new InvalidDataException("无法打开图像；请使用 BMP、PNG、JPEG 或 TIFF。");
        int depth = decoded.Depth() == MatType.CV_8U ? 8 : decoded.Depth() == MatType.CV_16U ? 16 : 0;
        if (depth == 0 || decoded.Channels() is not (1 or 3 or 4)) throw new InvalidDataException("首版支持 8/16 位灰度及彩色图像。");
        using var packed = new Mat();
        if (decoded.Channels() == 4) Cv2.CvtColor(decoded, packed, ColorConversionCodes.BGRA2BGR);
        else decoded.CopyTo(packed);
        int width = packed.Width, height = packed.Height, channels = packed.Channels();
        int stride = checked(width * channels * depth / 8);
        int length = StandaloneCameraFrame.RequiredBytes(width, height, depth, channels, stride);
        var pixels = new byte[length];
        for (int y = 0; y < height; y++) Marshal.Copy(packed.Ptr(y), pixels, y * stride, stride);
        return new(new(pixels, width, height, depth, channels, stride, DateTimeOffset.Now), Path.GetFullPath(path));
    }

    public T Read<T>(Func<HImage, T> reader)
    {
        var pinned = GCHandle.Alloc(Data.Pixels, GCHandleType.Pinned);
        try
        {
            return reader(new HImage { cols = Data.Width, rows = Data.Height, depth = Data.BitDepth, channels = Data.Channels, stride = Data.Stride, pData = pinned.AddrOfPinnedObject(), isDispose = true });
        }
        finally { pinned.Free(); }
    }

    public BitmapSource CreateBitmap()
    {
        byte[] pixels = Data.Pixels;
        PixelFormat format;
        if (Data.Channels == 1) format = Data.BitDepth == 8 ? PixelFormats.Gray8 : PixelFormats.Gray16;
        else if (Data.BitDepth == 8) format = Data.Channels == 3 ? PixelFormats.Bgr24 : PixelFormats.Bgra32;
        else
        {
            if (Data.Channels != 3) throw new InvalidDataException("不支持 16 位四通道预览。");
            format = PixelFormats.Rgb48;
            pixels = (byte[])pixels.Clone();
            for (int y = 0; y < Data.Height; y++)
                for (int x = 0; x < Data.Width; x++)
                {
                    int p = y * Data.Stride + x * 6;
                    (pixels[p], pixels[p + 4]) = (pixels[p + 4], pixels[p]);
                    (pixels[p + 1], pixels[p + 5]) = (pixels[p + 5], pixels[p + 1]);
                }
        }
        var bitmap = BitmapSource.Create(Data.Width, Data.Height, 96, 96, format, null, pixels, Data.Stride);
        bitmap.Freeze();
        return bitmap;
    }
}

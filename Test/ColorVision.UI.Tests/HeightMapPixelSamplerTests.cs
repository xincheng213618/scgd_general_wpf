using ColorVision.ImageEditor;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace ColorVision.UI.Tests;

public sealed class HeightMapPixelSamplerTests
{
    private static readonly MethodInfo ConvertBitmapToGray = typeof(Window3D).GetMethod(
        "ConvertBitmapToGray",
        BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void CalculateFitSize_PreservesAspectRatioWithinConfiguredBounds()
    {
        Type samplerType = typeof(Window3D).Assembly.GetType(
            "ColorVision.ImageEditor.EditorTools.ThreeD.HeightMapPixelSampler",
            throwOnError: true)!;
        MethodInfo calculateFitSize = samplerType.GetMethod(
            "CalculateFitSize",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var size = ((int Width, int Height))calculateFitSize.Invoke(null, [5544, 3692, 512, 512])!;

        Assert.Equal(512, size.Width);
        Assert.Equal(341, size.Height);
    }

    [Fact]
    public void ConvertBitmapToGray_DoesNotUpscaleSmallOpaqueImages()
    {
        RunOnStaThread(() =>
        {
            byte[] source =
            [
                0, 10, 20, 30,
                40, 50, 60, 70,
                80, 90, 100, 110,
            ];
            WriteableBitmap bitmap = CreateBitmap(4, 3, PixelFormats.Gray8, source, 4);

            var sample = InvokeSampler(bitmap, 512, 512);

            Assert.Equal(4, sample.Width);
            Assert.Equal(3, sample.Height);
            Assert.Equal(source, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_UsesEdgeAlignedBilinearSampling()
    {
        RunOnStaThread(() =>
        {
            byte[] source =
            [
                0, 10, 20, 30,
                40, 50, 60, 70,
                80, 90, 100, 110,
            ];
            WriteableBitmap bitmap = CreateBitmap(4, 3, PixelFormats.Gray8, source, 4);

            var sample = InvokeSampler(bitmap, 3, 2);

            Assert.Equal(3, sample.Width);
            Assert.Equal(2, sample.Height);
            Assert.Equal(new byte[] { 0, 15, 30, 80, 95, 110 }, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_PreservesStraightColorAndAlphaSemantics()
    {
        RunOnStaThread(() =>
        {
            byte[] source =
            [
                0, 0, 255, 255,
                0, 255, 0, 0,
                255, 0, 0, 128,
                255, 255, 255, 255,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Bgra32, source, 8);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 76, 150, 29, 255 }, sample.Gray);
            Assert.Equal(new byte[] { 255, 0, 128, 255 }, sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_HandlesBgr32AsOpaqueColor()
    {
        RunOnStaThread(() =>
        {
            byte[] source =
            [
                0, 0, 255, 0,
                0, 255, 0, 0,
                255, 0, 0, 0,
                255, 255, 255, 0,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Bgr32, source, 8);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 76, 150, 29, 255 }, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_HandlesRgb48WithoutAFullSizeIntermediate()
    {
        RunOnStaThread(() =>
        {
            byte[] source =
            [
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
                0x40, 0x40, 0x40, 0x40, 0x40, 0x40,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Rgb48, source, 12);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 0, 255, 128, 64 }, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_UnpremultipliesPbgra32BeforeSampling()
    {
        RunOnStaThread(() =>
        {
            byte[] source =
            [
                25, 50, 100, 128,
                25, 50, 100, 128,
                25, 50, 100, 128,
                25, 50, 100, 128,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Pbgra32, source, 8);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 123, 123, 123, 123 }, sample.Gray);
            Assert.Equal(new byte[] { 128, 128, 128, 128 }, sample.Alpha);
        });
    }

    [Fact]
    public async Task BuildMeshCollections_CreatesFrozenCollectionsOffTheUiThread()
    {
        MethodInfo buildMeshCollections = typeof(Window3D).GetMethod(
            "BuildMeshCollections",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        byte[] pixels = [0, 64, 128, 255];

        object result = await Task.Run(() => buildMeshCollections.Invoke(
            null,
            new object?[] { pixels, null, 2, 2, 100.0 })!);
        var collections = ((Point3DCollection Positions, Int32Collection Indices,
            PointCollection TexCoords, Vector3DCollection Normals))result;

        Assert.True(collections.Positions.IsFrozen);
        Assert.True(collections.Indices.IsFrozen);
        Assert.True(collections.TexCoords.IsFrozen);
        Assert.True(collections.Normals.IsFrozen);
        Assert.Equal(4, collections.Positions.Count);
        Assert.Equal(6, collections.Indices.Count);
    }

    private static (byte[] Gray, byte[]? Alpha, int Width, int Height) InvokeSampler(
        WriteableBitmap bitmap,
        int maxWidth,
        int maxHeight)
    {
        return ((byte[] Gray, byte[]? Alpha, int Width, int Height))ConvertBitmapToGray.Invoke(
            null,
            [bitmap, maxWidth, maxHeight])!;
    }

    private static WriteableBitmap CreateBitmap(
        int width,
        int height,
        PixelFormat format,
        byte[] pixels,
        int stride)
    {
        WriteableBitmap bitmap = new(width, height, 96, 96, format, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        return bitmap;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

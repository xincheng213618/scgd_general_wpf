using ColorVision.ImageEditor;
using ColorVision.Core;
using OpenCvSharp;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class ImageAlgorithmPreviewSessionTests
{
    [Fact]
    public void SessionConstructionDoesNotAllocatePixelSizedManagedSnapshot()
    {
        RunOnStaThread(() =>
        {
            WriteableBitmap warmup = new(1, 1, 96, 96, PixelFormats.Rgb48, null);
            _ = CreateSession(warmup, new WriteableBitmap(warmup));

            WriteableBitmap source = new(512, 256, 96, 96, PixelFormats.Rgb48, null);
            WriteableBitmap preview = new(source);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            _ = CreateSession(source, preview);

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.True(allocatedBytes < 128 * 1024, $"Preview session allocated {allocatedBytes:N0} managed bytes.");
        });
    }

    [Fact]
    public void RepeatedApplyAlwaysStartsFromOriginalPixels()
    {
        RunOnStaThread(() =>
        {
            byte[] original = CreatePixels();
            WriteableBitmap bitmap = CreateBitmap(original);
            object session = CreateSession(bitmap);

            Apply(session, mat => mat.SetTo(Scalar.All(200)));
            Assert.All(ReadPixels(bitmap), value => Assert.Equal((byte)200, value));

            Apply(session, mat => Cv2.Add(mat, Scalar.All(1), mat));
            Assert.Equal(original.Select(value => (byte)(value + 1)), ReadPixels(bitmap));
        });
    }

    [Fact]
    public void ShowOriginalRestoresPixelsExactly()
    {
        RunOnStaThread(() =>
        {
            byte[] original = CreatePixels();
            WriteableBitmap bitmap = CreateBitmap(original);
            object session = CreateSession(bitmap);

            Apply(session, mat => Cv2.BitwiseNot(mat, mat));
            Invoke(session, "ShowOriginal");

            Assert.Equal(original, ReadPixels(bitmap));
        });
    }

    [Fact]
    public void FailedApplyUnlocksBitmapAndNextApplyStartsFromOriginal()
    {
        RunOnStaThread(() =>
        {
            byte[] original = CreatePixels();
            WriteableBitmap bitmap = CreateBitmap(original);
            object session = CreateSession(bitmap);

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() =>
                Apply(session, mat =>
                {
                    mat.SetTo(Scalar.All(77));
                    throw new InvalidOperationException("Expected test failure.");
                }));
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            Assert.All(ReadPixels(bitmap), value => Assert.Equal((byte)77, value));

            bitmap.Lock();
            bitmap.Unlock();

            Apply(session, mat => Cv2.Add(mat, Scalar.All(2), mat));
            Assert.Equal(original.Select(value => (byte)(value + 2)), ReadPixels(bitmap));
        });
    }

    [Fact]
    public void ApplyAfterCompletionDoesNothing()
    {
        RunOnStaThread(() =>
        {
            byte[] original = CreatePixels();
            WriteableBitmap bitmap = CreateBitmap(original);
            object session = CreateSession(bitmap);
            bool invoked = false;

            session.GetType().GetField("_isCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, true);
            Apply(session, _ => invoked = true);

            Assert.False(invoked);
            Assert.Equal(original, ReadPixels(bitmap));
        });
    }

    [Fact]
    public void SourceRevisionChangeAfterShowOriginalCancelsPreviewWithoutOverwritingNewSource()
    {
        RunOnStaThread(() =>
        {
            byte[] original = CreatePixels();
            WriteableBitmap source = CreateBitmap(original);
            DrawCanvas imageShow = new() { Source = source };
            ImageSource? viewSource = source;
            ImageSource? functionImage = null;
            long revision = 1;
            ImageProcessingContext context = CreateContext(
                imageShow,
                () => revision,
                value => value == revision,
                () => viewSource,
                value => viewSource = value,
                () => functionImage,
                value => functionImage = value);
            object session = StartSession(context);

            Apply(session, mat => mat.SetTo(Scalar.All(200)));
            Invoke(session, "ShowOriginal");
            revision++;
            bool invoked = false;

            Apply(session, _ => invoked = true);
            Invoke(session, "Commit");

            Assert.False(invoked);
            Assert.Same(source, viewSource);
            Assert.Same(source, imageShow.Source);
            Assert.Null(functionImage);
            imageShow.Dispose();
        });
    }

    [Fact]
    public void SourceRevisionChangeCancelsCommitWithoutOverwritingNewSource()
    {
        RunOnStaThread(() =>
        {
            WriteableBitmap source = CreateBitmap(CreatePixels());
            WriteableBitmap newerSource = CreateBitmap(Enumerable.Repeat((byte)42, 40).ToArray());
            DrawCanvas imageShow = new() { Source = source };
            ImageSource? viewSource = source;
            ImageSource? functionImage = null;
            long revision = 1;
            ImageProcessingContext context = CreateContext(
                imageShow,
                () => revision,
                value => value == revision,
                () => viewSource,
                value => viewSource = value,
                () => functionImage,
                value => functionImage = value);
            object session = StartSession(context);

            Apply(session, mat => mat.SetTo(Scalar.All(200)));
            viewSource = newerSource;
            imageShow.Source = newerSource;
            revision++;

            Invoke(session, "Commit");

            Assert.Same(newerSource, viewSource);
            Assert.Same(newerSource, imageShow.Source);
            Assert.Null(functionImage);
            imageShow.Dispose();
        });
    }

    private static object CreateSession(WriteableBitmap bitmap)
    {
        return CreateSession(new WriteableBitmap(bitmap), bitmap);
    }

    private static object CreateSession(BitmapSource originalSource, WriteableBitmap previewBitmap)
    {
        Type sessionType = typeof(ImageProcessingContext).Assembly.GetType(
            "ColorVision.ImageEditor.Algorithms.ImageAlgorithmPreviewSession",
            throwOnError: true)!;
        ConstructorInfo constructor = sessionType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(ImageProcessingContext), typeof(BitmapSource), typeof(WriteableBitmap)],
            modifiers: null)!;
        return constructor.Invoke([null, originalSource, previewBitmap]);
    }

    private static object StartSession(ImageProcessingContext context)
    {
        Type sessionType = typeof(ImageProcessingContext).Assembly.GetType(
            "ColorVision.ImageEditor.Algorithms.ImageAlgorithmPreviewSession",
            throwOnError: true)!;
        MethodInfo start = sessionType.GetMethod("Start", BindingFlags.Static | BindingFlags.Public)!
            ?? throw new InvalidOperationException("Missing preview session Start method.");
        return start.Invoke(null, [context])!;
    }

    private static ImageProcessingContext CreateContext(
        DrawCanvas imageShow,
        Func<long> getRevision,
        Func<long, bool> isCurrentRevision,
        Func<ImageSource?> getViewSource,
        Action<ImageSource?> setViewSource,
        Func<ImageSource?> getFunctionImage,
        Action<ImageSource?> setFunctionImage)
    {
        Guid documentInstanceId = Guid.NewGuid();
        Type bindingType = typeof(ImageProcessingContext).Assembly.GetType(
            "ColorVision.ImageEditor.ImageProcessingContextBinding",
            throwOnError: true)!;
        object binding = Activator.CreateInstance(bindingType, nonPublic: true)!;
        bindingType.GetProperty("IsInitialized")!.SetValue(binding, (Func<bool>)(() => true));
        bindingType.GetProperty("GetDocumentInstanceId")!.SetValue(binding, (Func<Guid>)(() => documentInstanceId));
        bindingType.GetProperty("IsDisposed")!.SetValue(binding, (Func<bool>)(() => false));
        bindingType.GetProperty("GetImageRevision")!.SetValue(binding, getRevision);
        bindingType.GetProperty("AcquireImageFrame")!.SetValue(binding, (Func<ImageFrameLease?>)(() =>
        {
            WriteableBitmap bitmap = getViewSource() as WriteableBitmap
                ?? throw new InvalidOperationException("The test context requires a writeable bitmap source.");
            HImage image = bitmap.ToHImage();
            SourceImageFrame frame = new(image, getRevision(), static ownedImage => ownedImage.Dispose());
            try
            {
                return frame.Acquire();
            }
            finally
            {
                frame.Dispose();
            }
        }));
        bindingType.GetProperty("IsCurrentImageRevision")!.SetValue(binding, isCurrentRevision);
        bindingType.GetProperty("NotifySourcePixelsChanged")!.SetValue(binding, (Action)(() => { }));
        bindingType.GetProperty("GetViewBitmapSource")!.SetValue(binding, getViewSource);
        bindingType.GetProperty("SetViewBitmapSource")!.SetValue(binding, setViewSource);
        bindingType.GetProperty("GetFunctionImage")!.SetValue(binding, getFunctionImage);
        bindingType.GetProperty("SetFunctionImage")!.SetValue(binding, setFunctionImage);
        bindingType.GetProperty("GetSelectedLayerSourceChannelIndex")!.SetValue(binding, (Func<int>)(() => 0));
        bindingType.GetProperty("SetImageSource")!.SetValue(binding, (Action<ImageSource>)(value => setViewSource(value)));
        bindingType.GetProperty("UpdateZoomAndScale")!.SetValue(binding, (Action)(() => { }));

        ConstructorInfo constructor = typeof(ImageProcessingContext).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(ImageViewConfig), typeof(DrawCanvas), typeof(System.Windows.Threading.Dispatcher), bindingType],
            modifiers: null)!;
        return (ImageProcessingContext)constructor.Invoke(
            [new ImageViewConfig(), imageShow, System.Windows.Threading.Dispatcher.CurrentDispatcher, binding]);
    }

    private static void Apply(object session, Action<Mat> apply)
    {
        Invoke(session, "Apply", apply);
    }

    private static void Invoke(object session, string methodName, params object[] arguments)
    {
        MethodInfo method = session.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)!
            ?? throw new InvalidOperationException($"Missing preview session method: {methodName}");
        method.Invoke(session, arguments);
    }

    private static byte[] CreatePixels()
    {
        return Enumerable.Range(0, 40).Select(index => (byte)(index * 3 + 5)).ToArray();
    }

    private static WriteableBitmap CreateBitmap(byte[] pixels)
    {
        WriteableBitmap bitmap = new(8, 5, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), pixels, bitmap.BackBufferStride, 0);
        return bitmap;
    }

    private static byte[] ReadPixels(WriteableBitmap bitmap)
    {
        byte[] pixels = new byte[bitmap.BackBufferStride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, bitmap.BackBufferStride, 0);
        return pixels;
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

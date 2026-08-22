using ColorVision.ImageEditor;
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

    private static object CreateSession(WriteableBitmap bitmap)
    {
        Type sessionType = typeof(ImageProcessingContext).Assembly.GetType(
            "ColorVision.ImageEditor.Algorithms.ImageAlgorithmPreviewSession",
            throwOnError: true)!;
        ConstructorInfo constructor = sessionType.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(ImageProcessingContext), typeof(WriteableBitmap)],
            modifiers: null)!;
        return constructor.Invoke([null, bitmap]);
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

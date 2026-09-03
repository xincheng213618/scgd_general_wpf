#if COLORVISION_WINDOW_RESIZE_DIAGNOSTICS
using ST.Library.UI;
using ST.Library.UI.NodeEditor;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Media;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ColorVision.UI.Tests;

/// <summary>Explicit diagnostic-build checks; no Window, Application, or Dispatcher loop is created.</summary>
public class STNodeEditorResizeDiagnosticsTests
{
    [Fact]
    public void Capture_DefaultOffThenEnabled_RecordsFrameMetadata()
    {
        StaTest.Run(() =>
        {
            using var editor = CreateEditor();
            Assert.Null(editor.DiagnosticBuffer);
            var initial = editor.GetResizeDiagnosticCapture();
            Assert.False(initial.IsCapturing);
            Assert.False(initial.IsDisposed);
            Assert.Equal(2048, initial.Capacity);
            Assert.Empty(initial.Samples);
            Assert.Equal(0L, initial.DroppedSamples);
            Assert.Null(editor.DiagnosticBuffer);

            editor.RenderFrame();
            Assert.NotNull(editor.RenderBitmap);
            Assert.Empty(editor.GetResizeDiagnosticCapture().Samples);
            Assert.Null(editor.DiagnosticBuffer);

            editor.BeginResizeDiagnosticCapture(Deadline());
            Assert.NotNull(editor.DiagnosticBuffer);
            editor.RenderFrame();
            var capture = editor.GetResizeDiagnosticCapture();
            Assert.True(capture.IsCapturing);
            Assert.Equal(0L, capture.DroppedSamples);
            var sample = Assert.Single(capture.Samples);
            AssertSuccessfulSample(sample, 192, 128, editor);
            Assert.False(sample.BufferRecreated);
        });
    }

    [Fact]
    public void Capture_SameSizeReusesResizeRecreates_AndPreservesPixels()
    {
        StaTest.Run(() =>
        {
            using var editor = CreateEditor();
            editor.RenderFrame();
            Bitmap originalBuffer = editor.RenderBitmap;
            string disabledPixels = editor.PixelDigest();

            editor.BeginResizeDiagnosticCapture(Deadline());
            editor.RenderFrame();
            Assert.Same(originalBuffer, editor.RenderBitmap);
            Assert.Equal(disabledPixels, editor.PixelDigest());
            editor.RenderFrame();
            var sameSize = editor.GetResizeDiagnosticCapture();
            Assert.Equal(2, sameSize.Samples.Length);
            Assert.All(sameSize.Samples, sample => Assert.False(sample.BufferRecreated));

            editor.ClientSize = new Size(256, 176);
            editor.RenderFrame();
            Assert.NotSame(originalBuffer, editor.RenderBitmap);
            var resized = editor.GetResizeDiagnosticCapture();
            Assert.Equal(3, resized.Samples.Length);
            Assert.True(resized.Samples[2].BufferRecreated);
            AssertSuccessfulSample(resized.Samples[2], 256, 176, editor);
            string resizedPixels = editor.PixelDigest();

            editor.StopResizeDiagnosticCapture();
            editor.RenderFrame();
            Assert.Equal(resizedPixels, editor.PixelDigest());
            Assert.Equal(3, editor.GetResizeDiagnosticCapture().Samples.Length);
        });
    }

    [Fact]
    public void Capture_StopExtendAndDeadlineExpiry_PreservesEarlierSamples()
    {
        StaTest.Run(() =>
        {
            using var editor = CreateEditor();
            long earlier = Deadline(30);
            long later = earlier + 30 * Stopwatch.Frequency;
            editor.BeginResizeDiagnosticCapture(earlier);
            editor.RenderFrame();
            var before = editor.GetResizeDiagnosticCapture();
            long firstStart = Assert.Single(before.Samples).StartTicks;

            editor.BeginResizeDiagnosticCapture(later);
            editor.BeginResizeDiagnosticCapture(earlier);
            Assert.True(editor.GetResizeDiagnosticCapture().UntilTimestamp >= later);
            Assert.Equal(firstStart, Assert.Single(editor.GetResizeDiagnosticCapture().Samples).StartTicks);
            editor.RenderFrame();
            Assert.Equal(2, editor.GetResizeDiagnosticCapture().Samples.Length);
            Assert.Single(before.Samples);

            editor.StopResizeDiagnosticCapture();
            editor.RenderFrame();
            var stopped = editor.GetResizeDiagnosticCapture();
            Assert.False(stopped.IsCapturing);
            Assert.Equal(0L, stopped.UntilTimestamp);
            Assert.Equal(2, stopped.Samples.Length);

            // Exercise the real timestamp gate without changing private state or pumping WPF.
            long expires = Stopwatch.GetTimestamp() + Math.Max(1, Stopwatch.Frequency / 100);
            editor.BeginResizeDiagnosticCapture(expires);
            Assert.True(SpinWait.SpinUntil(() => Stopwatch.GetTimestamp() >= expires, TimeSpan.FromSeconds(1)));
            editor.RenderFrame();
            var expired = editor.GetResizeDiagnosticCapture();
            Assert.False(expired.IsCapturing);
            Assert.Equal(2, expired.Samples.Length);
            Assert.Equal(firstStart, expired.Samples[0].StartTicks);
            Assert.Equal(0L, expired.DroppedSamples);

            editor.BeginResizeDiagnosticCapture(Deadline());
            editor.RenderFrame();
            Assert.Equal(3, editor.GetResizeDiagnosticCapture().Samples.Length);
        });
    }

    [Fact]
    public void Capture_CapacityAndDispose_AreBoundedAndReleaseState()
    {
        StaTest.Run(() =>
        {
            using var editor = CreateEditor(32, 24, addNode: false);
            using var other = CreateEditor(32, 24, addNode: false);
            editor.ShowGrid = false;
            editor.BeginResizeDiagnosticCapture(Deadline(120));
            int capacity = editor.GetResizeDiagnosticCapture().Capacity;
            Assert.Equal(2048, capacity);
            Array storage = Assert.IsAssignableFrom<Array>(editor.DiagnosticBuffer);
            Assert.Equal(capacity, storage.Length);

            for (int index = 0; index < capacity; index++) editor.RenderFrame();
            var full = editor.GetResizeDiagnosticCapture();
            Assert.Equal(capacity, full.Samples.Length);
            Assert.Equal(0L, full.DroppedSamples);
            long firstStart = full.Samples[0].StartTicks;
            long lastStart = full.Samples[capacity - 1].StartTicks;
            for (int index = 0; index < 5; index++) editor.RenderFrame();
            var overflow = editor.GetResizeDiagnosticCapture();
            Assert.Equal(capacity, overflow.Samples.Length);
            Assert.Equal(5L, overflow.DroppedSamples);
            Assert.Equal(firstStart, overflow.Samples[0].StartTicks);
            Assert.Equal(lastStart, overflow.Samples[capacity - 1].StartTicks);
            Assert.Same(storage, editor.DiagnosticBuffer);

            other.RenderFrame();
            Assert.Empty(other.GetResizeDiagnosticCapture().Samples);
            Assert.Null(other.DiagnosticBuffer);

            editor.Dispose();
            var disposed = editor.GetResizeDiagnosticCapture();
            Assert.True(disposed.IsDisposed);
            Assert.False(disposed.IsCapturing);
            Assert.Equal(0L, disposed.UntilTimestamp);
            Assert.Equal(0L, disposed.DroppedSamples);
            Assert.Empty(disposed.Samples);
            Assert.Null(editor.DiagnosticBuffer);
            editor.BeginResizeDiagnosticCapture(Deadline());
            editor.RenderFrame();
            Assert.Empty(editor.GetResizeDiagnosticCapture().Samples);
            Assert.Null(editor.DiagnosticBuffer);
        });
    }

    [Fact]
    public void Capture_DrawFailure_RecordsFailureAndRethrowsOriginal()
    {
        StaTest.Run(() =>
        {
            using var editor = CreateEditor(addNode: false);
            var expected = new InvalidOperationException("Synthetic drawing failure.");
            var node = new ThrowingNode(expected);
            node.Create();
            node.Left = node.Top = 20;
            editor.Nodes.Add(node);
            editor.BeginResizeDiagnosticCapture(Deadline());

            var actual = Assert.Throws<InvalidOperationException>(editor.RenderFrame);
            Assert.Same(expected, actual);
            var sample = Assert.Single(editor.GetResizeDiagnosticCapture().Samples);
            Assert.False(sample.Succeeded);
            Assert.True(sample.StartTicks > 0);
            Assert.True(sample.EndTicks >= sample.StartTicks);
            Assert.True(sample.EnsureEndTicks >= sample.EnsureStartTicks);
            Assert.Equal(0L, sample.DrawEndTicks);
            Assert.Equal(0L, sample.CopyStartTicks);
            Assert.Equal(0L, sample.CopyEndTicks);

            node.ShouldThrow = false;
            editor.RenderFrame();
            var recovered = editor.GetResizeDiagnosticCapture();
            Assert.Equal(2, recovered.Samples.Length);
            AssertSuccessfulSample(recovered.Samples[1], 192, 128, editor);
        });
    }

    private static void AssertSuccessfulSample(STRenderDiagnosticSample sample, int width, int height, STNodeEditor editor)
    {
        Assert.True(sample.Succeeded);
        Assert.True(sample.StartTicks > 0);
        Assert.True(sample.StartTicks <= sample.EnsureStartTicks && sample.EnsureStartTicks <= sample.EnsureEndTicks
            && sample.EnsureEndTicks <= sample.DrawEndTicks && sample.DrawEndTicks <= sample.CopyStartTicks
            && sample.CopyStartTicks <= sample.CopyEndTicks && sample.CopyEndTicks <= sample.EndTicks);
        Assert.Equal((double)width, sample.LogicalWidth);
        Assert.Equal((double)height, sample.LogicalHeight);
        Assert.True(sample.DpiScaleX > 0 && sample.DpiScaleY > 0);
        Assert.Equal((int)Math.Ceiling(width * sample.DpiScaleX), sample.PixelWidth);
        Assert.Equal((int)Math.Ceiling(height * sample.DpiScaleY), sample.PixelHeight);
        Assert.Equal(editor.CanvasScale, sample.CanvasScale);
        Assert.Equal(editor.CanvasOffsetX, sample.CanvasOffsetX);
        Assert.Equal(editor.CanvasOffsetY, sample.CanvasOffsetY);
        Assert.Equal(editor.Nodes.Count, sample.NodesCount);
    }

    private static long Deadline(int seconds = 60) => Stopwatch.GetTimestamp() + seconds * Stopwatch.Frequency;

    private static ProbeEditor CreateEditor(int width = 192, int height = 128, bool addNode = true)
    {
        var editor = new ProbeEditor
        {
            ClientSize = new Size(width, height),
            LimitCanvasToContentBounds = false,
            ShowBorder = false,
            ShowNodeShadow = false,
            ShowLocation = false,
            ShowCanvasDragLockButton = false,
        };
        if (addNode)
        {
            var node = new STNodeHub();
            node.Create();
            node.Left = node.Top = 20;
            editor.Nodes.Add(node);
        }
        return editor;
    }

    private sealed class ProbeEditor : STNodeEditor
    {
        private static readonly FieldInfo RenderBitmapField = typeof(STNodeEditor).GetField("m_render_bitmap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly FieldInfo DiagnosticBufferField = typeof(STNodeEditor).GetField("m_resize_diagnostic_samples", BindingFlags.Instance | BindingFlags.NonPublic)!;

        internal Bitmap RenderBitmap => Assert.IsType<Bitmap>(RenderBitmapField.GetValue(this));
        internal object? DiagnosticBuffer => DiagnosticBufferField.GetValue(this);

        internal void RenderFrame()
        {
            var visual = new DrawingVisual();
            using DrawingContext context = visual.RenderOpen();
            base.OnRender(context);
        }

        // All pixel inspection occurs after OnRender and outside its diagnostic timestamps.
        internal string PixelDigest()
        {
            Bitmap bitmap = RenderBitmap;
            int rowBytes = checked(bitmap.Width * 4);
            byte[] pixels = new byte[checked(rowBytes * bitmap.Height)];
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppPArgb);
            try
            {
                for (int row = 0; row < bitmap.Height; row++)
                    Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), pixels, row * rowBytes, rowBytes);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return Convert.ToHexString(SHA256.HashData(pixels));
        }
    }

    private sealed class ThrowingNode(Exception expectedException) : STNode
    {
        internal bool ShouldThrow { get; set; } = true;

        protected override void OnDrawNode(DrawingTools tools)
        {
            if (ShouldThrow) throw expectedException;
            base.OnDrawNode(tools);
        }
    }
}
#endif

using ColorVision.ImageEditor.Draw;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;

namespace Conoscope.Tests;

public sealed class FocusCircleRenderOptimizationTests
{
    [Fact]
    public void BoundaryConstraintUsesTheAttributeRenderWithoutAnExtraFrame()
    {
        RunOnStaThread(() =>
        {
            using ConoscopeImageHost host = new();
            host.SetFocusCircleBoundary(new Point(0, 0), 100);
            CountingCircleText circle = new(new CircleTextProperties
            {
                Center = new Point(120, 0),
                Radius = 10,
                RadiusY = 20,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.DeepSkyBlue, 2),
                Text = "Focus_1",
            });

            host.ConstrainFocusCircleToBoundary(circle);

            Assert.Equal(1, circle.RenderCount);
            Assert.Equal(new Point(80, 0), circle.Attribute.Center);
            Assert.Equal(10, circle.Attribute.Radius);
            Assert.Equal(20, circle.Attribute.RadiusY);
            Assert.NotNull(circle.Drawing);
        });
    }

    private sealed class CountingCircleText : DVCircleText
    {
        internal CountingCircleText(CircleTextProperties properties) : base(properties)
        {
        }

        internal int RenderCount { get; private set; }

        public override void Render()
        {
            RenderCount++;
            base.Render();
        }
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
        Assert.True(thread.TrySetApartmentState(ApartmentState.STA));
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA focus-circle test did not finish.");

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

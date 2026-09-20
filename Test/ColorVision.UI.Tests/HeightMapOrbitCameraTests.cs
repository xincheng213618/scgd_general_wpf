using ColorVision.ImageEditor.EditorTools.ThreeD;
using System.Windows.Media.Media3D;

namespace ColorVision.UI.Tests;

public sealed class HeightMapOrbitCameraTests
{
    [Theory]
    [InlineData(5544, 3692, 100, 1.777777777777778, false)]
    [InlineData(10652, 14204, 100, 1.777777777777778, false)]
    [InlineData(10652, 14204, 100, 0.5625, false)]
    [InlineData(5544, 3692, 100, 0.25, false)]
    [InlineData(5544, 3692, 100, 0.075, false)]
    [InlineData(1000, 750, 10000, 1.777777777777778, false)]
    [InlineData(1000, 750, 10000, 0.25, false)]
    [InlineData(1000, 750, 0, 1.777777777777778, false)]
    [InlineData(1, 1, 0, 1, false)]
    [InlineData(0, 0, 0, 1, false)]
    [InlineData(10652, 14204, 10000, 0.5625, true)]
    [InlineData(5544, 3692, 0, 0.075, true)]
    public void Fit_KeepsEveryBoundingCornerInsideActualViewport(
        double width, double height, double depth, double aspect, bool top)
    {
        var bounds = new Rect3D(-250, 91, -100, width, height, depth);
        var camera = new HeightMapOrbitCamera();

        camera.Fit(bounds, aspect, top, immediate: true);

        AssertPointClose(Center(bounds), camera.Position + camera.LookDirection);
        Assert.True(double.IsFinite(camera.Distance) && camera.Distance > 0);
        for (int cornerIndex = 0; cornerIndex < 8; cornerIndex++)
        {
            var corner = new Point3D(bounds.X + ((cornerIndex & 1) == 0 ? 0 : width),
                bounds.Y + ((cornerIndex & 2) == 0 ? 0 : height),
                bounds.Z + ((cornerIndex & 4) == 0 ? 0 : depth));
            var projected = Project(camera, corner, aspect);
            Assert.True(projected.Depth > 0, $"Corner {cornerIndex} is behind the camera.");
            Assert.True(Math.Abs(projected.X) <= 1 + 1e-10,
                $"Corner {cornerIndex} has horizontal NDC {projected.X} at aspect {aspect}.");
            Assert.True(Math.Abs(projected.Y) <= 1 + 1e-10,
                $"Corner {cornerIndex} has vertical NDC {projected.Y} at aspect {aspect}.");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fit_OrientatesImageTopUpAndImageRightRight(bool top)
    {
        var bounds = new Rect3D(0, 0, 0, 1000, 750, 100);
        var camera = new HeightMapOrbitCamera();
        camera.Fit(bounds, 4.0 / 3, top, immediate: true);
        Point3D center = Center(bounds);
        var imageTop = Project(camera, center + new Vector3D(0, 100, 0), 4.0 / 3);
        var imageBottom = Project(camera, center - new Vector3D(0, 100, 0), 4.0 / 3);
        var imageRight = Project(camera, center + new Vector3D(100, 0, 0), 4.0 / 3);
        var imageLeft = Project(camera, center - new Vector3D(100, 0, 0), 4.0 / 3);

        Assert.True(imageTop.Y > imageBottom.Y);
        Assert.True(imageRight.X > imageLeft.X);
        Assert.True(camera.Position.Z > center.Z);
        Assert.True(camera.UpDirection.Z > 0);
        Assert.InRange(Math.Abs(Vector3D.DotProduct(camera.UpDirection, camera.LookDirection)), 0, 1e-9);
    }

    [Fact]
    public void Fit_EmptyBoundsLeavesExistingViewAndPendingMotionIntact()
    {
        HeightMapOrbitCamera expected = CreateCamera();
        HeightMapOrbitCamera actual = CreateCamera();
        expected.Orbit(23, -14);
        actual.Orbit(23, -14);
        actual.Fit(Rect3D.Empty, 0.25, top: true, immediate: true);

        expected.Update(0.033);
        actual.Update(0.033);

        AssertPointClose(expected.Position, actual.Position);
        AssertVectorClose(expected.LookDirection, actual.LookDirection);
        AssertVectorClose(expected.UpDirection, actual.UpDirection);
    }

    [Fact]
    public void Update_UsesElapsedTimeConsistentlyAtThirtyAndSixtyFramesPerSecond()
    {
        HeightMapOrbitCamera faster = CreateCamera();
        HeightMapOrbitCamera slower = CreateCamera();
        foreach (HeightMapOrbitCamera camera in new[] { faster, slower })
        {
            camera.Orbit(70, -25);
            camera.Zoom(2);
            camera.Pan(40, -25, 750);
        }

        for (int frame = 0; frame < 12; frame++) Assert.True(faster.Update(0.0165));
        for (int frame = 0; frame < 6; frame++) Assert.True(slower.Update(0.033));

        AssertPointClose(faster.Position, slower.Position);
        AssertVectorClose(faster.LookDirection, slower.LookDirection);
        AssertVectorClose(faster.UpDirection, slower.UpDirection);
        Assert.InRange(Math.Abs(faster.Distance - slower.Distance), 0, 1e-9);
    }

    [Fact]
    public void OrbitAndZoom_KeepStableFocusAndZoomReturnsToOriginalDistance()
    {
        HeightMapOrbitCamera camera = CreateCamera();
        Point3D focus = camera.Position + camera.LookDirection;
        double originalDistance = camera.Distance;

        camera.Orbit(70, -25);
        camera.Zoom(2);
        for (int frame = 0; frame < 10; frame++)
        {
            camera.Update(0.033);
            AssertPointClose(focus, camera.Position + camera.LookDirection);
        }
        Settle(camera);
        Assert.True(camera.Distance < originalDistance);
        camera.Zoom(-2);
        Settle(camera);

        AssertPointClose(focus, camera.Position + camera.LookDirection);
        Assert.InRange(Math.Abs(originalDistance - camera.Distance), 0, 1e-9);
    }

    [Fact]
    public void Pan_TranslatesFocusInCameraPlaneByProjectedPixelDistance()
    {
        HeightMapOrbitCamera camera = CreateCamera();
        Point3D focus = camera.Position + camera.LookDirection;
        Vector3D forward = camera.LookDirection;
        forward.Normalize();
        Vector3D right = Vector3D.CrossProduct(forward, camera.UpDirection);
        right.Normalize();
        const double viewportHeight = 750;
        const double dx = 40;
        const double dy = -25;
        double distance = camera.Distance;
        double unitsPerPixel = 2 * distance * Math.Tan(45 * Math.PI / 360) / viewportHeight;
        Point3D expectedFocus = focus + (-right * dx + camera.UpDirection * dy) * unitsPerPixel;

        camera.Pan(dx, dy, viewportHeight);
        Settle(camera);

        AssertPointClose(expectedFocus, camera.Position + camera.LookDirection);
        Assert.InRange(Math.Abs(distance - camera.Distance), 0, 1e-9);
    }

    [Fact]
    public void Fit_ResetRestoresOrientationCenterAndDistanceAfterNavigation()
    {
        var bounds = new Rect3D(0, 0, 0, 1000, 750, 100);
        HeightMapOrbitCamera camera = CreateCamera();
        Point3D originalPosition = camera.Position;
        Vector3D originalLook = camera.LookDirection;
        Vector3D originalUp = camera.UpDirection;
        camera.Orbit(130, -100);
        camera.Zoom(4);
        camera.Pan(-80, 70, 750);
        Settle(camera);

        camera.Fit(bounds, 4.0 / 3);
        Settle(camera);

        AssertPointClose(originalPosition, camera.Position);
        AssertVectorClose(originalLook, camera.LookDirection);
        AssertVectorClose(originalUp, camera.UpDirection);
    }

    [Fact]
    public void Fit_ResetAfterWholeTurnsDoesNotStartAnotherRotation()
    {
        var bounds = new Rect3D(0, 0, 0, 1000, 750, 100);
        foreach (double turn in new[] { -720d, -360d, 360d, 720d })
        {
            HeightMapOrbitCamera camera = CreateCamera();
            Point3D originalPosition = camera.Position;
            Vector3D originalLook = camera.LookDirection;
            Vector3D originalUp = camera.UpDirection;
            camera.Orbit(turn, 0);
            Settle(camera);
            AssertPointClose(originalPosition, camera.Position);

            camera.Fit(bounds, 4.0 / 3);

            Assert.False(camera.Update(0.016), "Reset should already be at its target after complete turns.");
            AssertPointClose(originalPosition, camera.Position);
            AssertVectorClose(originalLook, camera.LookDirection);
            AssertVectorClose(originalUp, camera.UpDirection);
        }
    }

    private static HeightMapOrbitCamera CreateCamera()
    {
        var camera = new HeightMapOrbitCamera();
        camera.Fit(new Rect3D(0, 0, 0, 1000, 750, 100), 4.0 / 3, immediate: true);
        return camera;
    }

    private static void Settle(HeightMapOrbitCamera camera)
    {
        // Advance simulated time until the public coordinator reports its target reached.
        for (int step = 0; step < 100; step++)
            if (!camera.Update(0.033)) return;
        Assert.Fail("The camera did not settle within 3.3 seconds of simulated time.");
    }

    private static Point3D Center(Rect3D bounds) => new(bounds.X + bounds.SizeX / 2,
        bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);

    private static (double X, double Y, double Depth) Project(HeightMapOrbitCamera camera, Point3D point, double aspect)
    {
        Vector3D forward = camera.LookDirection;
        forward.Normalize();
        Vector3D up = camera.UpDirection;
        up.Normalize();
        Vector3D right = Vector3D.CrossProduct(forward, up);
        right.Normalize();
        Vector3D relative = point - camera.Position;
        double depth = Vector3D.DotProduct(relative, forward);
        // Helix SharpDX PerspectiveCamera's 45-degree FOV is vertical.
        double halfHeight = depth * Math.Tan(45 * Math.PI / 360);
        return (Vector3D.DotProduct(relative, right) / (halfHeight * aspect),
            Vector3D.DotProduct(relative, up) / halfHeight, depth);
    }

    private static void AssertPointClose(Point3D expected, Point3D actual) =>
        Assert.InRange((expected - actual).Length, 0, 1e-9);

    private static void AssertVectorClose(Vector3D expected, Vector3D actual) =>
        Assert.InRange((expected - actual).Length, 0, 1e-9);
}

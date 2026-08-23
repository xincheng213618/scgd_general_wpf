using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Annotations;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class DrawShapeCompatibilityTests
{
    [Fact]
    public void PublicConstructorsPreserveAttributeAndTextAttributeIdentity()
    {
        WpfTestHost.Invoke(() =>
        {
            CircleProperties circleProperties = new();
            DVCircle circle = new(circleProperties);
            Assert.Same(circleProperties, circle.Attribute);
            Assert.Same(circleProperties, circle.BaseAttribute);

            RectangleProperties rectangleProperties = new();
            DVRectangle rectangle = new(rectangleProperties);
            Assert.Same(rectangleProperties, rectangle.Attribute);
            Assert.Same(rectangleProperties, rectangle.BaseAttribute);

            CircleTextProperties circleTextProperties = new()
            {
                Pen = new Pen(Brushes.Red, 3),
                FontSize = 47,
            };
            TextAttribute circleTextAttribute = circleTextProperties.TextAttribute;
            DVCircleText circleText = new(circleTextProperties);
            Assert.Same(circleTextProperties, circleText.Attribute);
            Assert.Same(circleTextProperties, circleText.BaseAttribute);
            Assert.Same(circleTextAttribute, circleText.TextAttribute);
            Assert.Equal(30, circleTextProperties.FontSize);

            RectangleTextProperties rectangleTextProperties = new()
            {
                Pen = new Pen(Brushes.Red, 4),
                FontSize = 53,
            };
            TextAttribute rectangleTextAttribute = rectangleTextProperties.TextAttribute;
            DVRectangleText rectangleText = new(rectangleTextProperties);
            Assert.Same(rectangleTextProperties, rectangleText.Attribute);
            Assert.Same(rectangleTextProperties, rectangleText.BaseAttribute);
            Assert.Same(rectangleTextAttribute, rectangleText.TextAttribute);
            Assert.Equal(40, rectangleTextProperties.FontSize);

            Assert.NotNull(new DVCircle().Attribute);
            Assert.NotNull(new DVCircleText().Attribute);
            Assert.NotNull(new DVRectangle().Attribute);
            Assert.NotNull(new DVRectangleText().Attribute);
        });
    }

    [Fact]
    public void CircleBoundsKeepCircleAndEllipseSemantics()
    {
        WpfTestHost.Invoke(() =>
        {
            CircleProperties circleProperties = new()
            {
                Center = new Point(100, 80),
                Radius = 30,
                RadiusY = 12,
            };
            DVCircle circle = new(circleProperties);
            Assert.Equal(new Rect(70, 50, 60, 60), circle.GetRect());

            CircleTextProperties ellipseProperties = new()
            {
                Center = new Point(100, 80),
                Radius = 30,
                RadiusY = 12,
            };
            DVCircleText ellipse = new(ellipseProperties);
            Assert.Equal(new Rect(70, 68, 60, 24), ellipse.GetRect());

            Rect target = new(10, 20, 80, 40);
            circle.SetRect(target);
            Assert.Equal(new Point(50, 40), circle.Attribute.Center);
            Assert.Equal(20, circle.Attribute.Radius);
            Assert.Equal(20, circle.Attribute.RadiusY);
            Assert.Equal(new Rect(30, 20, 40, 40), circle.GetRect());

            ellipse.SetRect(target);
            Assert.Equal(new Point(50, 40), ellipse.Attribute.Center);
            Assert.Equal(40, ellipse.Attribute.Radius);
            Assert.Equal(20, ellipse.Attribute.RadiusY);
            Assert.Equal(target, ellipse.GetRect());
        });
    }

    [Fact]
    public void RectangleBoundsRemainTheGeometryRect()
    {
        WpfTestHost.Invoke(() =>
        {
            Rect rect = new(12, 24, 80, 36);
            DVRectangle rectangle = new(new RectangleProperties { Rect = rect });
            DVRectangleText rectangleText = new(new RectangleTextProperties
            {
                Rect = rect,
                Text = "outside label",
                Position = RectangleTextPosition.Right,
            });

            Assert.Equal(rect, rectangle.GetRect());
            Assert.Equal(rect, rectangleText.GetRect());
        });
    }

    [Fact]
    public void ShapePropertiesRaiseTheEstablishedExactPropertyNames()
    {
        CircleTextProperties circle = new();
        List<string?> circleChanges = new();
        circle.PropertyChanged += (_, e) => circleChanges.Add(e.PropertyName);

        circle.Center = new Point(5, 7);
        circle.Radius = 41;
        circle.RadiusY = 23;
        circle.Text = "C1";
        circle.Id = 9;

        Assert.Equal(
            new[]
            {
                nameof(CircleTextProperties.Center),
                nameof(CircleTextProperties.Radius),
                nameof(CircleTextProperties.RadiusY),
                nameof(CircleTextProperties.Text),
                nameof(CircleTextProperties.Id),
            },
            circleChanges);

        RectangleTextProperties rectangle = new();
        List<string?> rectangleChanges = new();
        rectangle.PropertyChanged += (_, e) => rectangleChanges.Add(e.PropertyName);

        rectangle.Rect = new Rect(1, 2, 30, 40);
        rectangle.Text = "R1";
        rectangle.Id = 11;

        Assert.Equal(
            new[]
            {
                nameof(RectangleTextProperties.Rect),
                nameof(RectangleTextProperties.Text),
                nameof(RectangleTextProperties.Id),
            },
            rectangleChanges);
    }

    [Fact]
    public void RadiusSetterContinuesToSynchronizeRadiusYWithoutAnExtraNotification()
    {
        CircleProperties properties = new() { RadiusY = 8 };
        List<string?> changes = new();
        properties.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        properties.Radius = 27;

        Assert.Equal(27, properties.Radius);
        Assert.Equal(27, properties.RadiusY);
        Assert.Equal(new[] { nameof(CircleProperties.Radius) }, changes);
    }

    [Fact]
    public void AnnotationTextStyleNullContinuesToDiscriminatePlainAndTextShapes()
    {
        WpfTestHost.Invoke(() =>
        {
            CircleAnnotationItem plainCircle = CreateCircleItem(null);
            Assert.IsType<CircleProperties>(AnnotationMapper.ToProperties(plainCircle));
            Assert.IsType<DVCircle>(AnnotationMapper.ToVisual(plainCircle));

            CircleAnnotationItem textCircle = CreateCircleItem(new AnnotationTextStyle
            {
                Text = string.Empty,
                Visible = false,
                FontSize = 37.5,
            });
            Assert.IsType<CircleTextProperties>(AnnotationMapper.ToProperties(textCircle));
            DVCircleText circleVisual = Assert.IsType<DVCircleText>(AnnotationMapper.ToVisual(textCircle));
            Assert.Equal(37.5, circleVisual.TextAttribute.FontSize);
            Assert.False(circleVisual.Attribute.IsShowText);

            RectangleAnnotationItem plainRectangle = CreateRectangleItem(null);
            Assert.IsType<RectangleProperties>(AnnotationMapper.ToProperties(plainRectangle));
            Assert.IsType<DVRectangle>(AnnotationMapper.ToVisual(plainRectangle));

            RectangleAnnotationItem textRectangle = CreateRectangleItem(new AnnotationTextStyle
            {
                Text = string.Empty,
                Visible = false,
                FontSize = 42.25,
            });
            Assert.IsType<RectangleTextProperties>(AnnotationMapper.ToProperties(textRectangle));
            DVRectangleText rectangleVisual = Assert.IsType<DVRectangleText>(AnnotationMapper.ToVisual(textRectangle));
            Assert.Equal(42.25, rectangleVisual.TextAttribute.FontSize);
            Assert.False(rectangleVisual.Attribute.IsShowText);
        });
    }

    [Fact]
    public void ExistingSerializedEnumValuesRemainStable()
    {
        Assert.Equal(0, (int)RectangleTextPosition.Center);
        Assert.Equal(1, (int)RectangleTextPosition.Top);
        Assert.Equal(2, (int)RectangleTextPosition.Bottom);
        Assert.Equal(3, (int)RectangleTextPosition.Left);
        Assert.Equal(4, (int)RectangleTextPosition.Right);

        Assert.Equal(0, (int)AnnotationRectangleTextPosition.Center);
        Assert.Equal(1, (int)AnnotationRectangleTextPosition.Top);
        Assert.Equal(2, (int)AnnotationRectangleTextPosition.Bottom);
        Assert.Equal(3, (int)AnnotationRectangleTextPosition.Left);
        Assert.Equal(4, (int)AnnotationRectangleTextPosition.Right);

        Assert.Equal(0, (int)AnnotationKind.Circle);
        Assert.Equal(1, (int)AnnotationKind.Rectangle);
        Assert.Equal(2, (int)AnnotationKind.Text);
        Assert.Equal(3, (int)AnnotationKind.Line);
        Assert.Equal(4, (int)AnnotationKind.Polygon);
        Assert.Equal(5, (int)AnnotationKind.BezierCurve);
    }

    [Theory]
    [InlineData(typeof(CircleManager), "DrawCircleCache")]
    [InlineData(typeof(RectangleManager), "DrawingRectangleCache")]
    public void ShapeCreationRemovedByVisualsAddKeepsTheDetachedDraftForLegacyPluginTracking(Type managerType, string cacheFieldName)
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { Child = canvas, ContentMatrix = Matrix.Identity };
            DrawEditorContext context = new(canvas, zoombox);
            SelectEditorVisual selection = new(context);
            context.SelectionVisual = selection;
            IDrawEditorToggleTool manager = Assert.IsAssignableFrom<IDrawEditorToggleTool>(Activator.CreateInstance(managerType, context));
            IDisposable disposableManager = Assert.IsAssignableFrom<IDisposable>(manager);
            IDrawingVisual? detachedVisual = null;
            int propertyChangeCount = 0;
            canvas.VisualsAdd += RemoveCreatedShape;

            try
            {
                manager.IsChecked = true;
                MethodInfo beginDraw = managerType.GetMethod("OnBeginDraw", BindingFlags.Instance | BindingFlags.NonPublic)!;
                MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.PreviewMouseDownEvent,
                };
                beginDraw.Invoke(manager, new object[] { new Point(20, 30), args });

                Assert.Empty(canvas.Visuals.OfType<IDrawingVisual>());
                Assert.Same(detachedVisual, managerType.GetField(cacheFieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager));

                MethodInfo updateDraw = managerType.GetMethod("OnUpdateDraw", BindingFlags.Instance | BindingFlags.NonPublic)!;
                MouseEventArgs moveArgs = new(Mouse.PrimaryDevice, Environment.TickCount)
                {
                    RoutedEvent = Mouse.MouseMoveEvent,
                };
                updateDraw.Invoke(manager, new object[] { new Point(90, 100), moveArgs });

                Assert.True(propertyChangeCount > 0);
            }
            finally
            {
                canvas.VisualsAdd -= RemoveCreatedShape;
                disposableManager.Dispose();
                selection.Dispose();
                canvas.Dispose();
            }

            void RemoveCreatedShape(object? sender, VisualChangedEventArgs e)
            {
                if (e.Visual is IDrawingVisual visual)
                {
                    detachedVisual = visual;
                    visual.BaseAttribute.PropertyChanged += (_, _) => propertyChangeCount++;
                    canvas.RemoveVisualCommand((Visual)visual);
                }
            }
        });
    }

    private static CircleAnnotationItem CreateCircleItem(AnnotationTextStyle? textStyle)
    {
        return new CircleAnnotationItem
        {
            Center = new AnnotationPoint { X = 50, Y = 60 },
            RadiusX = 20,
            RadiusY = 12,
            Style = new AnnotationShapeStyle
            {
                FillColor = "Transparent",
                StrokeColor = "Red",
                StrokeThickness = 2,
            },
            TextStyle = textStyle,
        };
    }

    private static RectangleAnnotationItem CreateRectangleItem(AnnotationTextStyle? textStyle)
    {
        return new RectangleAnnotationItem
        {
            Rect = new AnnotationRect { X = 10, Y = 20, Width = 80, Height = 40 },
            Style = new AnnotationShapeStyle
            {
                FillColor = "Transparent",
                StrokeColor = "Red",
                StrokeThickness = 3,
            },
            TextStyle = textStyle,
            TextPosition = AnnotationRectangleTextPosition.Top,
        };
    }
}

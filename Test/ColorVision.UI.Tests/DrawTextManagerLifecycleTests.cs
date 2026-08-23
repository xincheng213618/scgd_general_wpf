using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public sealed class DrawTextManagerLifecycleTests
{
    [Fact]
    public void DisposingActiveManagerClearsCurrentTool()
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            fixture.Manager.IsChecked = true;

            Assert.Same(fixture.Manager, fixture.Context.DrawEditorManager.Current);

            fixture.Manager.Dispose();

            Assert.False(fixture.Manager.IsChecked);
            Assert.Null(fixture.Context.DrawEditorManager.Current);
        });
    }

    [Fact]
    public void DisposingInactiveManagerDoesNotClearAnotherToolsSelectionOrMouseCapture()
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            Window window = fixture.ShowHost();
            DVLine selectedLine = new(new LineProperties
            {
                Points = [new Point(20, 20), new Point(80, 60)],
                Pen = new Pen(Brushes.Red, 2),
            });
            try
            {
                selectedLine.Render();
                fixture.DrawCanvas.AddVisual(selectedLine);
                fixture.Selection.SetRender(selectedLine);
                fixture.DrawCanvas.CaptureMouse();
                Assert.True(fixture.DrawCanvas.IsMouseCaptured);

                fixture.Manager.Dispose();

                Assert.Same(selectedLine, Assert.Single(fixture.Selection.SelectVisuals));
                Assert.True(fixture.DrawCanvas.IsMouseCaptured);
            }
            finally
            {
                fixture.DrawCanvas.ReleaseMouseCapture();
                window.Close();
            }
        });
    }

    [Fact]
    public void DeactivatingDuringPendingCreationRemovesVisualAndCreationHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            fixture.Manager.IsChecked = true;
            InvokeMouseDown(fixture.Manager, fixture.DrawCanvas);

            Assert.Single(fixture.DrawCanvas.Visuals.OfType<DVText>());
            Assert.Single(fixture.DrawCanvas.UndoStack);
            ActionCommand unrelatedCommand = new(() => { }, () => { });
            fixture.DrawCanvas.AddActionCommand(unrelatedCommand);

            fixture.Manager.IsChecked = false;

            Assert.Empty(fixture.DrawCanvas.Visuals.OfType<DVText>());
            Assert.Same(unrelatedCommand, Assert.Single(fixture.DrawCanvas.UndoStack));
            Assert.Empty(fixture.DrawCanvas.RedoStack);
            Assert.False(GetPrivateField<bool>(fixture.Manager, "IsMouseDown"));
            Assert.Null(GetPrivateField<DVText?>(fixture.Manager, "TextCache"));
            Assert.Null(fixture.Context.DrawEditorManager.Current);
        });
    }

    [Fact]
    public void MouseUpTransfersCreationToEditorAndCancellingRemovesTheWholeCreation()
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            fixture.Manager.Config.DefaultText = string.Empty;
            fixture.Manager.IsChecked = true;

            InvokeMouseDown(fixture.Manager, fixture.DrawCanvas);
            DVText createdText = Assert.Single(fixture.DrawCanvas.Visuals.OfType<DVText>());
            Assert.NotNull(GetPrivateField<ActionCommand?>(fixture.Manager, "PendingCreationCommand"));

            InvokeMouseUp(fixture.Manager, fixture.DrawCanvas);

            Assert.True(createdText.IsEditing);
            Assert.Null(GetPrivateField<DVText?>(fixture.Manager, "TextCache"));
            Assert.Null(GetPrivateField<ActionCommand?>(fixture.Manager, "PendingCreationCommand"));
            Assert.Single(fixture.DrawCanvas.UndoStack);

            createdText.EndEdit(false);

            Assert.False(createdText.IsEditing);
            Assert.False(fixture.DrawCanvas.ContainsVisual(createdText));
            Assert.Empty(fixture.DrawCanvas.UndoStack);
            Assert.Empty(fixture.DrawCanvas.RedoStack);
        });
    }

    [Fact]
    public void UndoBeforeMouseUpCancelsPendingTextWithoutOpeningADetachedEditor()
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            fixture.Manager.IsChecked = true;
            InvokeMouseDown(fixture.Manager, fixture.DrawCanvas);
            Assert.Single(fixture.DrawCanvas.Visuals.OfType<DVText>());

            fixture.DrawCanvas.Undo();
            MouseButtonEventArgs mouseUp = InvokeMouseUp(fixture.Manager, fixture.DrawCanvas);

            Assert.Empty(fixture.DrawCanvas.Visuals.OfType<DVText>());
            Assert.Empty(fixture.DrawCanvas.UndoStack);
            Assert.Empty(fixture.DrawCanvas.RedoStack);
            Assert.Null(GetPrivateField<DVText?>(fixture.Manager, "TextCache"));
            Assert.Null(GetPrivateField<ActionCommand?>(fixture.Manager, "PendingCreationCommand"));
            Assert.False(GetPrivateField<bool>(fixture.Manager, "IsMouseDown"));
            Assert.False(mouseUp.Handled);
            Assert.Empty(fixture.EditorOverlay.Children);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisualsAddRemovalCannotLeavePendingTextOrCreationHistory(bool useRemovalCommand)
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            fixture.DrawCanvas.VisualsAdd += RemoveCreatedText;
            fixture.Manager.IsChecked = true;

            InvokeMouseDown(fixture.Manager, fixture.DrawCanvas);

            Assert.Empty(fixture.DrawCanvas.Visuals.OfType<DVText>());
            Assert.Empty(fixture.DrawCanvas.UndoStack);
            Assert.Empty(fixture.DrawCanvas.RedoStack);
            Assert.Null(GetPrivateField<DVText?>(fixture.Manager, "TextCache"));
            Assert.Null(GetPrivateField<ActionCommand?>(fixture.Manager, "PendingCreationCommand"));
            Assert.False(GetPrivateField<bool>(fixture.Manager, "IsMouseDown"));

            fixture.DrawCanvas.VisualsAdd -= RemoveCreatedText;

            void RemoveCreatedText(object? sender, VisualChangedEventArgs e)
            {
                if (e.Visual is DVText text)
                {
                    if (useRemovalCommand)
                        fixture.DrawCanvas.RemoveVisualCommand(text);
                    else
                        fixture.DrawCanvas.RemoveVisual(text);
                }
            }
        });
    }

    [Fact]
    public void RightMouseUpDoesNotCompleteOrConsumePendingTextCreation()
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            fixture.Manager.Config.DefaultText = string.Empty;
            fixture.Manager.IsChecked = true;
            InvokeMouseDown(fixture.Manager, fixture.DrawCanvas);
            DVText createdText = Assert.Single(fixture.DrawCanvas.Visuals.OfType<DVText>());

            MouseButtonEventArgs rightMouseUp = InvokeMouseUp(fixture.Manager, fixture.DrawCanvas, MouseButton.Right);

            Assert.False(rightMouseUp.Handled);
            Assert.True(GetPrivateField<bool>(fixture.Manager, "IsMouseDown"));
            Assert.Same(createdText, GetPrivateField<DVText?>(fixture.Manager, "TextCache"));
            Assert.False(createdText.IsEditing);

            MouseButtonEventArgs leftMouseUp = InvokeMouseUp(fixture.Manager, fixture.DrawCanvas);
            Assert.True(leftMouseUp.Handled);
            Assert.True(createdText.IsEditing);
            createdText.EndEdit(false);
        });
    }

    [Fact]
    public void ReplacingConfigMovesPropertyChangedSubscription()
    {
        WpfTestHost.Invoke(() =>
        {
            using TextManagerFixture fixture = new();
            DefaultTextStyleConfig defaultStyle = DefaultTextStyleConfig.Current;
            double originalFontSize = defaultStyle.FontSize;
            TextManagerConfig originalConfig = fixture.Manager.Config;
            TextManagerConfig replacement = new() { DefaultFontSize = originalFontSize + 5 };

            try
            {
                double firstFontSize = originalFontSize + 3;
                originalConfig.DefaultFontSize = firstFontSize;
                Assert.Equal(firstFontSize, defaultStyle.FontSize);

                fixture.Manager.Config = replacement;
                Assert.Equal(originalFontSize + 5, replacement.DefaultFontSize);

                originalConfig.DefaultFontSize = firstFontSize + 3;
                Assert.Equal(firstFontSize, defaultStyle.FontSize);

                double replacementFontSize = firstFontSize + 6;
                replacement.DefaultFontSize = replacementFontSize;
                Assert.Equal(replacementFontSize, defaultStyle.FontSize);

                int notificationCount = 0;
                replacement.PropertyChanged += (_, _) => notificationCount++;
                replacement.DefaultFontSize = replacementFontSize;
                Assert.Equal(0, notificationCount);
            }
            finally
            {
                fixture.Manager.Dispose();
                defaultStyle.FontSize = originalFontSize;
            }
        });
    }

    private static void InvokeMouseDown(TextManager manager, DrawCanvas drawCanvas)
    {
        MethodInfo method = typeof(TextManager).GetMethod("PreviewMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(TextManager).FullName, "PreviewMouseLeftButtonDown");
        MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left);
        args.RoutedEvent = Mouse.PreviewMouseDownEvent;
        method.Invoke(manager, new object[] { drawCanvas, args });
    }

    private static MouseButtonEventArgs InvokeMouseUp(TextManager manager, DrawCanvas drawCanvas, MouseButton button = MouseButton.Left)
    {
        MethodInfo method = typeof(TextManager).GetMethod("Image_PreviewMouseUp", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(TextManager).FullName, "Image_PreviewMouseUp");
        MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, button);
        args.RoutedEvent = Mouse.PreviewMouseUpEvent;
        method.Invoke(manager, new object[] { drawCanvas, args });
        return args;
    }

    private static T GetPrivateField<T>(TextManager manager, string fieldName)
    {
        FieldInfo field = typeof(TextManager).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(TextManager).FullName, fieldName);
        return (T)field.GetValue(manager)!;
    }

    private sealed class TextManagerFixture : IDisposable
    {
        private readonly SelectEditorVisual _selection;
        private readonly Grid _host;

        public TextManagerFixture()
        {
            DrawCanvas = new DrawCanvas
            {
                Width = 400,
                Height = 300,
                IsLayoutUpdated = false,
            };
            Zoombox zoombox = new()
            {
                Width = 400,
                Height = 300,
                Child = DrawCanvas,
                ContentMatrix = Matrix.Identity,
            };
            EditorOverlay = new Canvas { Width = 400, Height = 300 };
            _host = new Grid { Width = 400, Height = 300 };
            _host.Children.Add(zoombox);
            _host.Children.Add(EditorOverlay);
            _host.Measure(new Size(400, 300));
            _host.Arrange(new Rect(0, 0, 400, 300));
            _host.UpdateLayout();

            DrawEditorContext drawContext = new(DrawCanvas, zoombox);
            _selection = new SelectEditorVisual(drawContext);
            drawContext.SelectionVisual = _selection;
            Context = new TextEditingContext(
                drawContext.Id,
                DrawCanvas,
                zoombox,
                EditorOverlay,
                _selection,
                drawContext.DrawEditorManager,
                new ObservableCollection<IDrawingVisual>());
            _selection.TextEditingContext = Context;
            Manager = new TextManager(Context);
        }

        public DrawCanvas DrawCanvas { get; }

        public Canvas EditorOverlay { get; }

        public SelectEditorVisual Selection => _selection;

        public TextEditingContext Context { get; }

        public TextManager Manager { get; }

        public Window ShowHost()
        {
            Window window = new()
            {
                Content = _host,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 400,
                Height = 300,
                Left = -10000,
                Top = -10000,
            };
            window.Show();
            return window;
        }

        public void Dispose()
        {
            Manager.Dispose();
            _selection.Dispose();
            DrawCanvas.Dispose();
        }
    }
}

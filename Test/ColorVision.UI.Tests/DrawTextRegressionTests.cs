using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Annotations;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class DrawTextRegressionTests
{
    [Fact]
    public void HiddenStandaloneTextDoesNotRenderGlyphs()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);

            properties.IsShowText = true;
            text.Render();
            Assert.True(CountNonTransparentPixels(text) > 0);

            properties.IsShowText = false;
            text.Render();
            Assert.Equal(0, CountNonTransparentPixels(text));
        });
    }

    [Fact]
    public void StandaloneTextParticipatesInTheSharedVisibilityContract()
    {
        Assert.IsAssignableFrom<ITextProperties>(new TextProperties());
    }

    [Fact]
    public void LayoutScaleDoesNotRewriteTheDocumentFontSize()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.FontSize = 24;
            DVText text = new(properties);

            text.ApplyLayoutScale(new DrawingVisualScaleContext(true, 0.25, 0));
            Assert.Equal(24, properties.FontSize);

            text.ApplyLayoutScale(new DrawingVisualScaleContext(true, 4, 0));
            Assert.Equal(24, properties.FontSize);
        });
    }

    [Fact]
    public void DirectTextAttributeChangesRefreshTheVisualBounds()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            CountingText text = new(properties);
            text.Render();
            Rect initialBounds = text.GetRect();
            text.ResetRenderCount();

            properties.TextAttribute.Text = "A substantially longer annotation";

            Assert.True(text.RenderCount > 0);
            Assert.True(text.GetRect().Width > initialBounds.Width);

            double initialHeight = text.GetRect().Height;
            text.ResetRenderCount();
            properties.TextAttribute.FontSize = 36;

            Assert.True(text.RenderCount > 0);
            Assert.True(text.GetRect().Height > initialHeight);
        });
    }

    [Fact]
    public void ReassigningTheSameModelTextDoesNotOverwriteAnEditingDraft()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "未提交的中文 draft";

                properties.Text = properties.Text;

                Assert.Equal("未提交的中文 draft", editor.Text);
                Assert.Equal("Inspection note", properties.Text);
                text.EndEdit(false);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void MetadataChangesDoNotRepeatTextLayout()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            CountingText text = new(properties);
            text.Render();
            text.ResetRenderCount();

            properties.Id = 12;
            properties.Name = "Operator note";
            properties.Msg = "metadata";
            properties.Pen = new Pen(Brushes.Transparent, 2);

            Assert.Equal(0, text.RenderCount);

            properties.Background = Brushes.Navy;
            Assert.Equal(1, text.RenderCount);
        });
    }

    [Fact]
    public void BeginningAndCancellingEditDoesNotRewriteDocumentBounds()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Position = new Point(24, 36);
            properties.FontSize = 20;
            DVText text = new(properties);
            text.Render();
            Rect documentBounds = text.GetRect();

            DrawCanvas drawCanvas = new()
            {
                Width = 400,
                Height = 300,
                IsLayoutUpdated = false,
            };
            Zoombox zoombox = new()
            {
                Width = 400,
                Height = 300,
                Child = drawCanvas,
                ContentMatrix = new Matrix(2, 0, 0, 2, 0, 0),
            };
            Canvas editorOverlay = new() { Width = 400, Height = 300 };
            Grid host = new() { Width = 400, Height = 300 };
            host.Children.Add(zoombox);
            host.Children.Add(editorOverlay);
            host.Measure(new Size(400, 300));
            host.Arrange(new Rect(0, 0, 400, 300));
            host.UpdateLayout();

            DrawEditorContext drawContext = new(drawCanvas, zoombox);
            SelectEditorVisual selection = new(drawContext);
            drawContext.SelectionVisual = selection;
            ObservableCollection<IDrawingVisual> visuals = new() { text };
            TextEditingContext editingContext = new(
                drawContext.Id,
                drawCanvas,
                zoombox,
                editorOverlay,
                selection,
                drawContext.DrawEditorManager,
                visuals);
            selection.TextEditingContext = editingContext;
            drawCanvas.AddVisual(text);

            try
            {
                text.BeginEdit(editingContext);
                Assert.Equal(documentBounds, properties.Rect);

                text.EndEdit(false);
                Assert.Equal(documentBounds, properties.Rect);
            }
            finally
            {
                if (text.IsEditing)
                    text.EndEdit(false);
                selection.Dispose();
                drawCanvas.Dispose();
            }
        });
    }

    [Fact]
    public void EditingCommitsOrCancelsTextWithoutChangingTheDocumentPosition()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Position = new Point(24, 36);
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Committed 中文 text";
                text.EndEdit(true);

                Assert.Equal("Committed 中文 text", properties.Text);
                Assert.Equal(new Point(24, 36), properties.Position);

                text.BeginEdit(fixture.Context);
                editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Cancelled text";
                text.EndEdit(false);

                Assert.Equal("Committed 中文 text", properties.Text);
                Assert.Equal(new Point(24, 36), properties.Position);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void DoubleClickingTextDoesNotLeaveSelectionGestureActive()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                SelectEditorVisual selection = fixture.Context.SelectionVisual;
                selection.EditorContext.IsImageEditMode = true;
                MouseButtonEventArgs args = new(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Mouse.PreviewMouseDownEvent,
                };
                typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!.SetValue(args, 2);
                Point clickPoint = args.GetPosition(fixture.Context.DrawCanvas);
                properties.Position = new Point(clickPoint.X - 1, clickPoint.Y - 1);
                text.Render();
                Assert.True(text.GetRect().Contains(clickPoint));
                selection.SetRender(text);
                MethodInfo mouseDown = typeof(SelectEditorVisual).GetMethod("DrawCanvas_PreviewMouseLeftButtonDown", BindingFlags.Instance | BindingFlags.NonPublic)!;

                mouseDown.Invoke(selection, [fixture.Context.DrawCanvas, args]);

                Assert.True(args.Handled);
                Assert.True(text.IsEditing);
                FieldInfo gestureState = typeof(SelectEditorVisual).GetField("IsMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.False(Assert.IsType<bool>(gestureState.GetValue(selection)));
            }
            finally
            {
                if (text.IsEditing)
                    text.EndEdit(false);
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void EditingAcceptsMultilineUnicodeAndExpandsTheEditor()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                double singleLineHeight = editor.Height;
                const string multilineText = "第一行\r\nSecond line\r\n第三行";

                editor.Text = multilineText;

                Assert.True(editor.AcceptsReturn);
                Assert.True(editor.Height > singleLineHeight);
                text.EndEdit(true);
                Assert.Equal(multilineText, properties.Text);
                Assert.True(text.GetRect().Height > singleLineHeight);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    public void TrailingNewlineKeepsTheEmptyCaretLineInTextBounds(string lineBreak)
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Text = "First line";
            DVText text = new(properties);
            text.Render();
            double singleLineHeight = text.GetRect().Height;
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "First line" + lineBreak;

                Assert.True(editor.Height > singleLineHeight * 1.5);
                text.EndEdit(true);
                Assert.True(text.GetRect().Height > singleLineHeight * 1.5);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void LosingEditorFocusCommitsWithoutReselectingTheOldText()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Commit on focus change";
                int selectionChangeCount = 0;
                fixture.Context.SelectionVisual.SelectionChanged += (_, _) => selectionChangeCount++;
                Button destination = new();

                InvokeLostKeyboardFocus(text, editor, destination);

                Assert.False(text.IsEditing);
                Assert.Equal("Commit on focus change", properties.Text);
                Assert.Empty(fixture.Context.SelectionVisual.SelectVisuals);
                Assert.Equal(0, selectionChangeCount);
                Assert.Single(fixture.Context.DrawCanvas.UndoStack);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void OpeningTheEditorContextMenuKeepsEditingUntilFocusActuallyLeaves()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Before paste";
                ContextMenu contextMenu = new() { PlacementTarget = editor };
                MenuItem pasteItem = new();
                contextMenu.Items.Add(pasteItem);

                InvokeLostKeyboardFocus(text, editor, contextMenu);
                InvokeLostKeyboardFocus(text, editor, pasteItem);

                Assert.True(text.IsEditing);
                Assert.Same(editor, Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Pasted 中文 content";

                InvokeLostKeyboardFocus(text, editor, new Button());

                Assert.False(text.IsEditing);
                Assert.Equal("Pasted 中文 content", properties.Text);
                Assert.Single(fixture.Context.DrawCanvas.UndoStack);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void LosingFocusAfterClearingTextPreservesTheNewSelection()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            string originalText = properties.Text;
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = string.Empty;
                DVLine destination = CreateDestinationLine();
                fixture.Context.DrawCanvas.AddVisual(destination);
                fixture.Context.SelectionVisual.SetRender(destination);

                InvokeLostKeyboardFocus(text, editor, new Button());

                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Same(destination, Assert.Single(fixture.Context.SelectionVisual.SelectVisuals));
                Assert.Single(fixture.Context.DrawCanvas.UndoStack);

                fixture.Context.DrawCanvas.Undo();
                Assert.True(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Equal(originalText, properties.Text);
                Assert.Same(destination, Assert.Single(fixture.Context.SelectionVisual.SelectVisuals));
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void LosingFocusFromANewBlankTextPreservesTheNewSelectionAndDiscardsCreationHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Text = string.Empty;
            DVText text = new(properties);
            TextEditFixture fixture = new(text, addVisual: false);

            try
            {
                fixture.Context.DrawCanvas.AddVisualCommand(text);
                MethodInfo trackCreation = typeof(DVText).GetMethod("TrackCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
                trackCreation.Invoke(text, [fixture.Context.DrawCanvas.UndoStack[^1]]);
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                DVLine destination = CreateDestinationLine();
                fixture.Context.DrawCanvas.AddVisual(destination);
                fixture.Context.SelectionVisual.SetRender(destination);

                InvokeLostKeyboardFocus(text, editor, new Button());

                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Same(destination, Assert.Single(fixture.Context.SelectionVisual.SelectVisuals));
                Assert.Empty(fixture.Context.DrawCanvas.UndoStack);
                Assert.Empty(fixture.Context.DrawCanvas.RedoStack);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void UnloadingEditorHostCommitsAndDetachesTheEditingSession()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Committed before host unload";

                fixture.EditorOverlay.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));

                Assert.False(text.IsEditing);
                Assert.False(properties.IsEditing);
                Assert.Empty(fixture.EditorOverlay.Children);
                Assert.Equal("Committed before host unload", properties.Text);
                Assert.Single(fixture.Context.DrawCanvas.UndoStack);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void StandaloneTextSelectionDoesNotExposeNonFunctionalResizeHandles()
    {
        WpfTestHost.Invoke(() =>
        {
            DrawCanvas canvas = new();
            Zoombox zoombox = new() { ContentMatrix = Matrix.Identity };
            SelectEditorVisual selection = new(new DrawEditorContext(canvas, zoombox));
            DVText text = new(CreateProperties());
            text.Attribute.Position = new Point(30, 50);
            text.Render();
            selection.SelectVisuals.Add(text);
            selection.Render();

            Rect bounds = text.GetRect();
            Assert.True(selection.GetContainingRect(new Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2)));
            Assert.False(selection.GetContainingRect(new Point(bounds.Left - 3, bounds.Top - 3)));

            selection.Dispose();
            canvas.Dispose();
        });
    }

    [Fact]
    public void ActiveEditorTracksLayoutScaleWithoutChangingTheDocumentFontSize()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.FontSize = 20;
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                Assert.Equal(20, editor.FontSize);

                fixture.Context.Zoombox.ContentMatrix = new Matrix(2, 0, 0, 2, 0, 0);
                text.ApplyLayoutScale(new DrawingVisualScaleContext(true, 0.5, 0));

                Assert.Equal(10, editor.FontSize);
                Assert.Equal(20, properties.FontSize);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void ActiveEditorTracksDocumentStyleChangesWithoutRenderingTheHiddenVisual()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            CountingText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                text.ResetRenderCount();

                properties.FontSize = 30;
                properties.FontFamily = new FontFamily("Consolas");
                properties.FontStyle = FontStyles.Italic;
                properties.FontWeight = FontWeights.Bold;
                properties.FontStretch = FontStretches.Expanded;
                properties.FlowDirection = FlowDirection.RightToLeft;
                properties.Foreground = Brushes.Lime;
                properties.Background = Brushes.DarkBlue;

                Assert.Equal(30, editor.FontSize);
                Assert.Equal("Consolas", editor.FontFamily.Source);
                Assert.Equal(FontStyles.Italic, editor.FontStyle);
                Assert.Equal(FontWeights.Bold, editor.FontWeight);
                Assert.Equal(FontStretches.Expanded, editor.FontStretch);
                Assert.Equal(FlowDirection.RightToLeft, editor.FlowDirection);
                Assert.Same(Brushes.Lime, editor.Foreground);
                Assert.Same(Brushes.Lime, editor.CaretBrush);
                Assert.Same(Brushes.DarkBlue, editor.Background);
                Assert.Equal(0, text.RenderCount);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void ReplacingTextAttributeDuringEditRefreshesTheEditorAndCancelBaseline()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                TextAttribute replacement = new()
                {
                    Text = "外部替换文本",
                    FontSize = 28,
                    FontFamily = new FontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    Brush = Brushes.Orange,
                };

                properties.TextAttribute = replacement;

                Assert.Equal(replacement.Text, editor.Text);
                Assert.Equal(replacement.FontSize, editor.FontSize);
                Assert.Equal(replacement.FontFamily.Source, editor.FontFamily.Source);
                Assert.Equal(replacement.FontWeight, editor.FontWeight);
                Assert.Same(replacement.Brush, editor.Foreground);

                editor.Text = "local draft";
                text.EndEdit(false);

                Assert.Same(replacement, properties.TextAttribute);
                Assert.Equal("外部替换文本", properties.Text);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void RemovingAnActiveTextVisualTearsDownTheEditorSession()
    {
        WpfTestHost.Invoke(() =>
        {
            DVText text = new(CreateProperties());
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                Assert.True(text.IsEditing);
                Assert.Single(fixture.EditorOverlay.Children);

                fixture.Context.DrawCanvas.RemoveVisual(text);

                Assert.False(text.IsEditing);
                Assert.Empty(fixture.EditorOverlay.Children);

                fixture.Context.DrawCanvas.AddVisual(text);
                Assert.True(CountNonTransparentPixels(text) > 0);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void ActiveEditorTracksMutationsOfTheCurrentCanvasTransform()
    {
        WpfTestHost.Invoke(() =>
        {
            DVText text = new(CreateProperties());
            TextEditFixture fixture = new(text);
            RotateTransform rotation = new(0);
            fixture.Context.DrawCanvas.RenderTransform = rotation;
            fixture.Context.DrawCanvas.RenderTransformOrigin = new Point(0.5, 0.5);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                MatrixTransform editorTransform = Assert.IsType<MatrixTransform>(editor.RenderTransform);
                Matrix initial = editorTransform.Matrix;

                rotation.Angle = 90;
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

                MatrixTransform updatedTransform = Assert.IsType<MatrixTransform>(editor.RenderTransform);
                Matrix updated = updatedTransform.Matrix;
                Assert.Same(editorTransform, updatedTransform);
                Assert.NotEqual(initial, updated);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void ActiveEditorTracksCanvasTransformReplacementAndOriginChanges()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            DVText text = new(properties);
            TextEditFixture fixture = new(text);
            Point documentPosition = properties.Position;
            Rect documentBounds = properties.Rect;

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                MatrixTransform editorTransform = Assert.IsType<MatrixTransform>(editor.RenderTransform);
                Matrix initial = editorTransform.Matrix;

                fixture.Context.DrawCanvas.RenderTransformOrigin = new Point(0.5, 0.5);
                fixture.Context.DrawCanvas.RenderTransform = new RotateTransform(90);
                fixture.Context.DrawCanvas.UpdateLayout();
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                MatrixTransform rotatedTransform = Assert.IsType<MatrixTransform>(editor.RenderTransform);
                Matrix rotated = rotatedTransform.Matrix;

                fixture.Context.DrawCanvas.RenderTransform = new ScaleTransform(-1, 1);
                fixture.Context.DrawCanvas.UpdateLayout();
                editor.Dispatcher.Invoke(() => { }, DispatcherPriority.Loaded);
                MatrixTransform flippedTransform = Assert.IsType<MatrixTransform>(editor.RenderTransform);
                Matrix flipped = flippedTransform.Matrix;

                Assert.Same(editorTransform, rotatedTransform);
                Assert.Same(editorTransform, flippedTransform);
                Assert.NotEqual(initial, rotated);
                Assert.NotEqual(rotated, flipped);
                Assert.Equal(documentPosition, properties.Position);
                Assert.Equal(documentBounds, properties.Rect);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void ExternalUndoDuringEditingSynchronizesTheEditorAndKeepsRedoValid()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Text = "After";
            DVText text = new(properties);
            TextEditFixture fixture = new(text);
            fixture.Context.DrawCanvas.AddActionCommand(new ActionCommand(
                () => properties.Text = "Before",
                () => properties.Text = "After"));

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Before";

                fixture.Context.DrawCanvas.Undo();

                Assert.Equal("Before", properties.Text);
                Assert.Equal("Before", editor.Text);

                text.EndEdit(false);
                Assert.Equal("Before", properties.Text);
                Assert.Single(fixture.Context.DrawCanvas.RedoStack);

                fixture.Context.DrawCanvas.Redo();
                Assert.Equal("After", properties.Text);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void EndingEditAfterCanvasTransformIsFrozenCompletesCleanup()
    {
        WpfTestHost.Invoke(() =>
        {
            DVText text = new(CreateProperties());
            TextEditFixture fixture = new(text);
            RotateTransform rotation = new(10);
            fixture.Context.DrawCanvas.RenderTransform = rotation;

            try
            {
                text.BeginEdit(fixture.Context);
                rotation.Freeze();

                text.EndEdit(false);
                fixture.EditorOverlay.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

                Assert.False(text.IsEditing);
                Assert.Empty(fixture.EditorOverlay.Children);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void UndoingCreationDuringEditingDiscardsTheRedoGhost()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Text = string.Empty;
            DVText text = new(properties);
            TextEditFixture fixture = new(text, addVisual: false);

            try
            {
                fixture.Context.DrawCanvas.AddVisualCommand(text);
                MethodInfo trackCreation = typeof(DVText).GetMethod("TrackCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
                trackCreation.Invoke(text, new object[] { fixture.Context.DrawCanvas.UndoStack[^1] });

                text.BeginEdit(fixture.Context);
                fixture.Context.DrawCanvas.Undo();

                Assert.False(text.IsEditing);
                Assert.Empty(fixture.EditorOverlay.Children);
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Empty(fixture.Context.DrawCanvas.RedoStack);

                fixture.Context.DrawCanvas.Redo();
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void DeletingCreationDuringEditingKeepsCreationAndRemovalHistoryLinear()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Text = string.Empty;
            DVText text = new(properties);
            TextEditFixture fixture = new(text, addVisual: false);

            try
            {
                fixture.Context.DrawCanvas.AddVisualCommand(text);
                MethodInfo trackCreation = typeof(DVText).GetMethod("TrackCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
                trackCreation.Invoke(text, new object[] { fixture.Context.DrawCanvas.UndoStack[^1] });
                text.BeginEdit(fixture.Context);

                fixture.Context.DrawCanvas.RemoveVisualCommand(text);

                Assert.False(text.IsEditing);
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Equal(2, fixture.Context.DrawCanvas.UndoStack.Count);
                fixture.Context.DrawCanvas.Undo();
                Assert.True(fixture.Context.DrawCanvas.ContainsVisual(text));
                fixture.Context.DrawCanvas.Undo();
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                fixture.Context.DrawCanvas.Redo();
                Assert.True(fixture.Context.DrawCanvas.ContainsVisual(text));
                fixture.Context.DrawCanvas.Redo();
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void CancellingANewBlankTextRemovesItsCreationHistory()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Text = string.Empty;
            DVText text = new(properties);
            TextEditFixture fixture = new(text, addVisual: false);

            try
            {
                fixture.Context.DrawCanvas.AddVisualCommand(text);
                MethodInfo trackCreation = typeof(DVText).GetMethod("TrackCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
                trackCreation.Invoke(text, new object[] { fixture.Context.DrawCanvas.UndoStack[^1] });

                text.BeginEdit(fixture.Context);
                text.EndEdit(false);

                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Empty(fixture.Context.DrawCanvas.UndoStack);
                Assert.Empty(fixture.Context.DrawCanvas.RedoStack);

                fixture.Context.DrawCanvas.Undo();
                fixture.Context.DrawCanvas.Redo();
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void CancellingNewBlankTextPreservesUnrelatedHistoryWithoutRestoringAGhost()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            properties.Text = string.Empty;
            DVText text = new(properties);
            TextEditFixture fixture = new(text, addVisual: false);

            try
            {
                fixture.Context.DrawCanvas.AddVisualCommand(text);
                ActionCommand creationCommand = fixture.Context.DrawCanvas.UndoStack[^1];
                MethodInfo trackCreation = typeof(DVText).GetMethod("TrackCreationCommand", BindingFlags.Instance | BindingFlags.NonPublic)!;
                trackCreation.Invoke(text, new object[] { creationCommand });

                text.BeginEdit(fixture.Context);
                ActionCommand unrelatedCommand = new(() => { }, () => { });
                fixture.Context.DrawCanvas.AddActionCommand(unrelatedCommand);
                text.EndEdit(false);

                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Same(unrelatedCommand, Assert.Single(fixture.Context.DrawCanvas.UndoStack));

                fixture.Context.DrawCanvas.Undo();
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                fixture.Context.DrawCanvas.Redo();
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void ClearingExistingTextCanBeUndoneAndRedoneAsOneCommand()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            string originalText = properties.Text;
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = string.Empty;
                text.EndEdit(true);

                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Single(fixture.Context.DrawCanvas.UndoStack);

                fixture.Context.DrawCanvas.Undo();
                Assert.True(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Equal(originalText, properties.Text);

                fixture.Context.DrawCanvas.Redo();
                Assert.False(fixture.Context.DrawCanvas.ContainsVisual(text));
                Assert.Equal(string.Empty, properties.Text);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void EditingExistingTextCanBeUndoneAndRedoneAsOneCommand()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties properties = CreateProperties();
            string originalText = properties.Text;
            DVText text = new(properties);
            TextEditFixture fixture = new(text);

            try
            {
                text.BeginEdit(fixture.Context);
                TextBox editor = Assert.IsType<TextBox>(Assert.Single(fixture.EditorOverlay.Children));
                editor.Text = "Updated 中文 annotation";
                text.EndEdit(true);

                Assert.Equal("Updated 中文 annotation", properties.Text);
                Assert.Single(fixture.Context.DrawCanvas.UndoStack);

                fixture.Context.DrawCanvas.Undo();
                Assert.Equal(originalText, properties.Text);

                fixture.Context.DrawCanvas.Redo();
                Assert.Equal("Updated 中文 annotation", properties.Text);
            }
            finally
            {
                fixture.Dispose();
            }
        });
    }

    [Fact]
    public void AnnotationJsonRoundTripPreservesStandaloneTextDocumentState()
    {
        WpfTestHost.Invoke(() =>
        {
            TextProperties source = CreateProperties();
            source.Id = 17;
            source.Name = "Operator note";
            source.Msg = "metadata";
            source.Position = new Point(123.5, 67.25);
            source.IsShowText = false;
            source.Background = new SolidColorBrush(Color.FromArgb(160, 20, 30, 40));
            source.TextAttribute.FontSize = 27.5;
            source.TextAttribute.FontFamily = new FontFamily("Segoe UI");
            source.TextAttribute.FontStyle = FontStyles.Italic;
            source.TextAttribute.FontWeight = FontWeights.SemiBold;
            source.TextAttribute.FontStretch = FontStretches.Expanded;
            source.TextAttribute.FlowDirection = FlowDirection.RightToLeft;
            source.TextAttribute.Brush = new SolidColorBrush(Colors.AliceBlue) { Opacity = 0.4 };

            AnnotationDocument document = AnnotationMapper.CreateDocument(new BaseProperties[] { source });
            string json = AnnotationMapper.Serialize(document);
            AnnotationDocument restoredDocument = AnnotationMapper.Deserialize(json);
            DVText restoredVisual = Assert.IsType<DVText>(AnnotationMapper.ToVisual(Assert.Single(restoredDocument.Items)));
            TextProperties restored = restoredVisual.Attribute;

            Assert.Equal(source.Id, restored.Id);
            Assert.Equal(source.Name, restored.Name);
            Assert.Equal(source.Msg, restored.Msg);
            Assert.Equal(source.Position, restored.Position);
            Assert.Equal(source.IsShowText, restored.IsShowText);
            Assert.Equal(source.Text, restored.Text);
            Assert.Equal(source.FontSize, restored.FontSize);
            Assert.Equal(source.FontFamily.Source, restored.FontFamily.Source);
            Assert.Equal(source.FontStyle, restored.FontStyle);
            Assert.Equal(source.FontWeight, restored.FontWeight);
            Assert.Equal(source.FontStretch, restored.FontStretch);
            Assert.Equal(source.FlowDirection, restored.FlowDirection);
            Assert.Equal(GetEffectiveColor(source.Foreground), GetEffectiveColor(restored.Foreground));
            Assert.Equal(GetEffectiveColor(source.Background), GetEffectiveColor(restored.Background));
        });
    }

    private static TextProperties CreateProperties()
    {
        return new TextProperties
        {
            Text = "Inspection note",
            FontSize = 18,
            Position = new Point(12, 16),
            Foreground = Brushes.White,
            Background = Brushes.Transparent,
            Pen = new Pen(Brushes.Transparent, 1),
        };
    }

    private static DVLine CreateDestinationLine()
    {
        DVLine line = new(new LineProperties
        {
            Pen = new Pen(Brushes.Cyan, 2),
            Points = [new Point(40, 40), new Point(100, 80)],
        });
        line.Render();
        return line;
    }

    private static void InvokeLostKeyboardFocus(DVText text, TextBox editor, IInputElement destination)
    {
        KeyboardFocusChangedEventArgs args = new(Keyboard.PrimaryDevice, Environment.TickCount, editor, destination);
        MethodInfo lostFocus = typeof(DVText).GetMethod("OnEditorLostKeyboardFocus", BindingFlags.Instance | BindingFlags.NonPublic)!;
        lostFocus.Invoke(text, [editor, args]);
    }

    private static Color GetColor(Brush brush)
    {
        return Assert.IsType<SolidColorBrush>(brush).Color;
    }

    private static Color GetEffectiveColor(Brush brush)
    {
        SolidColorBrush solidColorBrush = Assert.IsType<SolidColorBrush>(brush);
        Color color = solidColorBrush.Color;
        byte effectiveAlpha = (byte)Math.Round(color.A * solidColorBrush.Opacity, MidpointRounding.AwayFromZero);
        return Color.FromArgb(effectiveAlpha, color.R, color.G, color.B);
    }

    private static int CountNonTransparentPixels(DrawingVisual visual)
    {
        const int width = 320;
        const int height = 120;
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        byte[] pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);

        int count = 0;
        for (int index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] != 0)
                count++;
        }
        return count;
    }

    private sealed class CountingText : DVText
    {
        public CountingText(TextProperties properties)
            : base(properties)
        {
        }

        public int RenderCount { get; private set; }

        public override void Render()
        {
            RenderCount++;
            base.Render();
        }

        public void ResetRenderCount()
        {
            RenderCount = 0;
        }
    }

    private sealed class TextEditFixture : IDisposable
    {
        private readonly DrawCanvas _drawCanvas;
        private readonly SelectEditorVisual _selection;

        public TextEditFixture(DVText text, bool addVisual = true)
        {
            _drawCanvas = new DrawCanvas
            {
                Width = 400,
                Height = 300,
                IsLayoutUpdated = false,
            };
            Zoombox zoombox = new()
            {
                Width = 400,
                Height = 300,
                Child = _drawCanvas,
                ContentMatrix = Matrix.Identity,
            };
            EditorOverlay = new Canvas { Width = 400, Height = 300 };
            Grid host = new() { Width = 400, Height = 300 };
            host.Children.Add(zoombox);
            host.Children.Add(EditorOverlay);
            host.Measure(new Size(400, 300));
            host.Arrange(new Rect(0, 0, 400, 300));
            host.UpdateLayout();

            DrawEditorContext drawContext = new(_drawCanvas, zoombox);
            _selection = new SelectEditorVisual(drawContext);
            drawContext.SelectionVisual = _selection;
            Context = new TextEditingContext(
                drawContext.Id,
                _drawCanvas,
                zoombox,
                EditorOverlay,
                _selection,
                drawContext.DrawEditorManager,
                new ObservableCollection<IDrawingVisual> { text });
            _selection.TextEditingContext = Context;
            if (addVisual)
            {
                _drawCanvas.AddVisual(text);
            }
        }

        public Canvas EditorOverlay { get; }

        public TextEditingContext Context { get; }

        public void Dispose()
        {
            _selection.Dispose();
            _drawCanvas.Dispose();
        }
    }
}

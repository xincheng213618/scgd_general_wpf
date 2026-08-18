#pragma warning disable CA1707
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests
{
    public class STNodeEditorCanvasTests
    {
        [Fact]
        public void CreateBezierPath_RoutesBackwardConnectionThroughOutsideGutter()
        {
            using System.Drawing.Drawing2D.GraphicsPath path =
                TestNodeEditor.CreateBezierPathForTest(300, 100, 100, 100, 0.3f);

            Assert.True(path.PointCount > 4);
            Assert.True(path.PathPoints.Max(point => point.Y) >= 170);
        }

        [Fact]
        public void CreateBezierPath_BackwardConnectionDoesNotUseSharedHorizontalSegment()
        {
            using System.Drawing.Drawing2D.GraphicsPath path =
                TestNodeEditor.CreateBezierPathForTest(700, 100, 400, 100, 0.3f);

            Assert.DoesNotContain(
                path.PathTypes.Skip(1),
                type => (type & (byte)System.Drawing.Drawing2D.PathPointType.PathTypeMask)
                    == (byte)System.Drawing.Drawing2D.PathPointType.Line);
            Assert.Equal(170, path.PathPoints.Max(point => point.Y));
        }

        [Fact]
        public void CanvasBounds_AreLimitedByDefault()
        {
            RunInSta(() =>
            {
                using var editor = CreateEditorWithNode();

                editor.MoveCanvas(10000f, -10000f, bAnimation: false, CanvasMoveArgs.All);

                Assert.NotEqual(10000f, editor.CanvasOffsetX);
                Assert.NotEqual(-10000f, editor.CanvasOffsetY);
            });
        }

        [Fact]
        public void InfiniteCanvas_AllowsMovementBeyondNodeBounds()
        {
            RunInSta(() =>
            {
                using var editor = CreateEditorWithNode();
                editor.LimitCanvasToContentBounds = false;

                editor.MoveCanvas(10000f, -10000f, bAnimation: false, CanvasMoveArgs.All);

                Assert.Equal(10000f, editor.CanvasOffsetX);
                Assert.Equal(-10000f, editor.CanvasOffsetY);
            });
        }

        [Fact]
        public void AnimationTimer_RemainsStoppedWhenLoadedEditorIsIdle()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor();
                Window window = ShowLoaded(editor);
                try
                {
                    Assert.False(GetAnimationTimer(editor).IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void AnimationTimer_StopsAfterAnimatedCanvasMovementCompletes()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor
                {
                    LimitCanvasToContentBounds = false
                };
                Window window = ShowLoaded(editor);
                try
                {
                    editor.MoveCanvas(120f, -80f, bAnimation: true, CanvasMoveArgs.All);

                    DispatcherTimer timer = GetAnimationTimer(editor);
                    Assert.True(timer.IsEnabled);
                    AdvanceAnimationUntilIdle(editor);
                    Assert.Equal(120f, editor.CanvasOffsetX);
                    Assert.Equal(-80f, editor.CanvasOffsetY);
                    Assert.False(timer.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void AnimationTimer_StopsAfterAlertFadeCompletes()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor();
                Window window = ShowLoaded(editor);
                try
                {
                    editor.ShowAlert(
                        "done",
                        System.Drawing.Color.White,
                        System.Drawing.Color.Black,
                        -1001,
                        AlertLocation.RightBottom,
                        bRedraw: false);

                    DispatcherTimer timer = GetAnimationTimer(editor);
                    Assert.True(timer.IsEnabled);
                    AdvanceAnimation(editor);
                    Assert.False(timer.IsEnabled);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void AutoCanvasDragMode_FollowsSelectedNodeCollection()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor
                {
                    AutoSwitchCanvasDragBySelection = true
                };
                var first = new TrackingNode();
                var second = new TrackingNode();
                first.Create();
                second.Create();
                editor.Nodes.Add(first);
                editor.Nodes.Add(second);

                Assert.True(editor.EnableBlankLeftDragCanvas);

                first.SetSelected(bSelected: true, bRedraw: false);
                Assert.False(editor.EnableBlankLeftDragCanvas);

                second.SetSelected(bSelected: true, bRedraw: false);
                first.SetSelected(bSelected: false, bRedraw: false);
                Assert.False(editor.EnableBlankLeftDragCanvas);

                second.SetSelected(bSelected: false, bRedraw: false);
                Assert.True(editor.EnableBlankLeftDragCanvas);
            });
        }

        [Fact]
        public void AutoCanvasDragMode_PreservesRectangleSelectionDecisionAfterClearingSelection()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor
                {
                    AutoSwitchCanvasDragBySelection = true
                };
                var node = new TrackingNode();
                node.Create();
                editor.Nodes.Add(node);
                node.SetSelected(bSelected: true, bRedraw: false);
                bool enableBlankLeftDragCanvasAtMouseDown = editor.EnableBlankLeftDragCanvas;

                node.SetSelected(bSelected: false, bRedraw: false);

                Assert.True(editor.EnableBlankLeftDragCanvas);
                Assert.False(TestNodeEditor.ShouldPanBlankCanvasForTest(
                    STMouseButtons.Left,
                    enableBlankLeftDragCanvasAtMouseDown,
                    System.Windows.Input.ModifierKeys.None));
            });
        }

        [Theory]
        [InlineData(true, System.Windows.Input.ModifierKeys.None, true)]
        [InlineData(true, System.Windows.Input.ModifierKeys.Control, false)]
        [InlineData(false, System.Windows.Input.ModifierKeys.None, false)]
        [InlineData(false, System.Windows.Input.ModifierKeys.Control, false)]
        public void ControlForcesBlankLeftDragIntoRectangleSelection(
            bool enableBlankLeftDragCanvas,
            System.Windows.Input.ModifierKeys modifiers,
            bool expectedPan)
        {
            Assert.Equal(
                expectedPan,
                TestNodeEditor.ShouldPanBlankCanvasForTest(
                    STMouseButtons.Left,
                    enableBlankLeftDragCanvas,
                    modifiers));
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, true, true)]
        public void ControlRectangleSelectionAddsNodesToTheExistingSelection(
            bool intersectsSelectionRectangle,
            bool wasSelectedBeforeDrag,
            bool expectedSelected)
        {
            Assert.Equal(
                expectedSelected,
                TestNodeEditor.ShouldSelectNodeFromRectangleForTest(
                    intersectsSelectionRectangle,
                    wasSelectedBeforeDrag));
        }

        [Fact]
        public void NodeActivation_RequiresLeftMouseButton()
        {
            Assert.True(TestNodeEditor.ShouldActivateNodeFromMouseForTest(STMouseButtons.Left));
            Assert.False(TestNodeEditor.ShouldActivateNodeFromMouseForTest(STMouseButtons.Right));
            Assert.False(TestNodeEditor.ShouldActivateNodeFromMouseForTest(STMouseButtons.Middle));
        }

        [Fact]
        public void PropertyEditorVisibility_RequiresOneActiveSelectedNode()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor();
                var first = new TrackingNode();
                var second = new TrackingNode();
                first.Create();
                second.Create();
                editor.Nodes.Add(first);
                editor.Nodes.Add(second);

                Assert.False(FlowEditorCanvas.ShouldShowPropertyEditor(editor));

                editor.SetActiveNode(first);
                Assert.True(FlowEditorCanvas.ShouldShowPropertyEditor(editor));

                second.SetSelected(bSelected: true, bRedraw: false);
                Assert.False(FlowEditorCanvas.ShouldShowPropertyEditor(editor));

                second.SetSelected(bSelected: false, bRedraw: false);
                editor.SetActiveNode(null);
                Assert.False(FlowEditorCanvas.ShouldShowPropertyEditor(editor));
            });
        }

        [Fact]
        public void PropertyEditorPosition_PrefersRightSideAndRespectsSafeArea()
        {
            var position = FlowEditorCanvas.CalculatePropertyPanelPosition(
                new System.Windows.Rect(100, 120, 80, 50),
                new System.Windows.Size(300, 260),
                new System.Windows.Size(900, 600),
                new System.Windows.Thickness(0, 54, 10, 20));

            Assert.Equal(190, position.X);
            Assert.Equal(120, position.Y);
        }

        [Fact]
        public void PropertyEditorPosition_FallsBackToLeftAndClampsVertically()
        {
            var position = FlowEditorCanvas.CalculatePropertyPanelPosition(
                new System.Windows.Rect(700, 500, 100, 50),
                new System.Windows.Size(300, 260),
                new System.Windows.Size(900, 600),
                new System.Windows.Thickness(0, 54, 10, 20));

            Assert.Equal(390, position.X);
            Assert.Equal(320, position.Y);
        }

        [Fact]
        public void PropertyEditorFirstRender_PositionsWhileHidden()
        {
            RunInSta(() =>
            {
                var panel = new System.Windows.Controls.Border
                {
                    Width = 300,
                    Height = 240,
                    Visibility = System.Windows.Visibility.Collapsed
                };
                bool positionedWhileHidden = false;

                FlowEditorCanvas.PreparePropertyPanelForFirstRender(
                    panel,
                    new System.Windows.Size(420, 520),
                    measuredSize =>
                    {
                        positionedWhileHidden = panel.Visibility == System.Windows.Visibility.Hidden;
                        Assert.Equal(300, measuredSize.Width);
                        Assert.Equal(240, measuredSize.Height);
                        System.Windows.Controls.Canvas.SetLeft(panel, 640);
                        System.Windows.Controls.Canvas.SetTop(panel, 320);
                    });

                Assert.True(positionedWhileHidden);
                Assert.Equal(System.Windows.Visibility.Visible, panel.Visibility);
                Assert.Equal(640, System.Windows.Controls.Canvas.GetLeft(panel));
                Assert.Equal(320, System.Windows.Controls.Canvas.GetTop(panel));
            });
        }

        [Fact]
        public void PropertyEditorPanel_LongContentDoesNotExceedMaximumWidth()
        {
            RunInSta(() =>
            {
                using var canvas = new FlowEditorCanvas();
                canvas.NodePropertyPanel.Children.Add(new System.Windows.Controls.TextBox
                {
                    Text = new string('W', 200),
                    Width = 2000
                });
                canvas.NodePropertyPanelContainer.Visibility = System.Windows.Visibility.Visible;

                canvas.Measure(new System.Windows.Size(1200, 800));
                canvas.Arrange(new System.Windows.Rect(0, 0, 1200, 800));
                canvas.UpdateLayout();

                Assert.Equal(
                    FlowEditorCanvas.PropertyPanelMaxWidth,
                    canvas.NodePropertyPanelContainer.MaxWidth);
                Assert.InRange(
                    canvas.NodePropertyPanelContainer.ActualWidth,
                    0,
                    FlowEditorCanvas.PropertyPanelMaxWidth);
            });
        }

        [Fact]
        public void NodeInspector_CanSwitchBetweenConfigurationAndDocumentation()
        {
            RunInSta(() =>
            {
                using var canvas = new FlowEditorCanvas();
                STNodeEditor editor = canvas.NodeEditor;
                var node = new TrackingNode();
                node.Create();
                editor.Nodes.Add(node);
                canvas.ShowNodeDocumentation(true);
                editor.SetActiveNode(node);

                Assert.True(canvas.IsShowingNodeDocumentation);
                Assert.Single(canvas.NodePropertyPanel.Children);
                Assert.IsType<System.Windows.Controls.StackPanel>(canvas.NodePropertyPanel.Children[0]);

                editor.SetActiveNode(null);
                canvas.ShowNodeDocumentation(false);

                Assert.False(canvas.IsShowingNodeDocumentation);
                Assert.Empty(canvas.NodePropertyPanel.Children);
            });
        }

        [Fact]
        public void LocalCameraDocumentation_ExplainsCalibrationAndFlipOrder()
        {
            var node = new LocalCameraNode();

            FlowNodeDocumentation documentation = FlowNodeDocumentationPresenter.GetDocumentation(node);

            Assert.Equal(Lang.GetOrDefault("Flow_LocalCamera_Processing"), documentation.Processing);
            Assert.Contains(documentation.Properties, property => property.Name == Lang.GetOrDefault("图像翻转"));
        }

        [Fact]
        public void LocalCameraDirectParameters_SetAllExposureChannels()
        {
            var node = new LocalCameraNode
            {
                ExpTime = 125.5f,
                Gain = 2.5f,
                AvgCount = 3
            };

            var cameraParameters = node.BuildCameraParameters();

            Assert.Equal(125.5f, cameraParameters.ExpTime);
            Assert.Equal(125.5f, cameraParameters.ExpTimeR);
            Assert.Equal(125.5f, cameraParameters.ExpTimeG);
            Assert.Equal(125.5f, cameraParameters.ExpTimeB);
            Assert.Equal(2.5f, cameraParameters.Gain);
            Assert.Equal(3, cameraParameters.AvgCount);
        }

        [Fact]
        public void LocalCameraDirectParameters_DefaultToOneHundredMilliseconds()
        {
            var node = new LocalCameraNode();

            var cameraParameters = node.BuildCameraParameters();

            Assert.Equal(100f, cameraParameters.ExpTime);
            Assert.Equal(100f, cameraParameters.ExpTimeR);
            Assert.Equal(100f, cameraParameters.ExpTimeG);
            Assert.Equal(100f, cameraParameters.ExpTimeB);
            Assert.Equal(0f, cameraParameters.Gain);
            Assert.Equal(1, cameraParameters.AvgCount);
        }

        [Fact]
        public void LocalCameraLoad_IgnoresRemovedLegacyTemplateProperty()
        {
            var node = new LocalCameraNode();
            node.Create();
            var savedProperties = new Dictionary<string, byte[]>
            {
                ["CamTempName"] = System.Text.Encoding.UTF8.GetBytes("Legacy.Template")
            };

            Exception? exception = Record.Exception(() => node.OnLoadNode(savedProperties));

            Assert.Null(exception);
            Assert.Null(typeof(LocalCameraNode).GetProperty("CamTempName"));
        }

        [Fact]
        public void NodeMovement_HidesEmbeddedPropertyEditor()
        {
            RunInSta(() =>
            {
                using var canvas = new FlowEditorCanvas();
                STNodeEditor editor = canvas.NodeEditor;
                var node = new TrackingNode();
                node.Create();
                editor.Nodes.Add(node);
                canvas.NodePropertyPanelContainer.Visibility = System.Windows.Visibility.Visible;

                node.Location = new System.Drawing.Point(30, 40);

                Assert.Equal(
                    System.Windows.Visibility.Collapsed,
                    canvas.NodePropertyPanelContainer.Visibility);
            });
        }

        [Fact]
        public void PropertyEditorRefresh_FromWorkerThread_IsDispatchedToOwner()
        {
            RunInSta(() =>
            {
                using var canvas = new FlowEditorCanvas();
                STNodeEditor editor = canvas.NodeEditor;
                var signPanel = canvas.NodePropertyPanel;
                signPanel.Children.Add(new System.Windows.Controls.Border());

                Exception? workerException = null;
                var worker = new Thread(() =>
                {
                    try
                    {
                        canvas.RefreshNodePropertyPanel();
                    }
                    catch (Exception ex)
                    {
                        workerException = ex;
                    }
                });
                worker.Start();
                worker.Join();

                Assert.Null(workerException);

                var frame = new System.Windows.Threading.DispatcherFrame();
                _ = editor.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.ContextIdle,
                    new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);

                Assert.Empty(signPanel.Children);
                Assert.Equal(System.Windows.Visibility.Collapsed, signPanel.Visibility);
            });
        }

        [Fact]
        public void ClearSelection_HidesPropertyEditorBeforeFlowRuns()
        {
            RunInSta(() =>
            {
                using var canvas = new FlowEditorCanvas();
                STNodeEditor editor = canvas.NodeEditor;
                var node = new TrackingNode();
                node.Create();
                editor.Nodes.Add(node);
                node.SetSelected(bSelected: true, bRedraw: false);
                canvas.NodePropertyPanelContainer.Visibility = System.Windows.Visibility.Visible;

                FlowEditorOperations.ClearSelection(editor);

                Assert.Null(editor.ActiveNode);
                Assert.Empty(editor.GetSelectedNode());
                Assert.Equal(
                    System.Windows.Visibility.Collapsed,
                    canvas.NodePropertyPanelContainer.Visibility);
            });
        }

        [Fact]
        public void ManualCanvasDragMode_RemainsTheCompatibleDefault()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor
                {
                    EnableBlankLeftDragCanvas = false
                };
                var node = new TrackingNode();
                node.Create();
                editor.Nodes.Add(node);

                node.SetSelected(bSelected: true, bRedraw: false);
                node.SetSelected(bSelected: false, bRedraw: false);

                Assert.False(editor.AutoSwitchCanvasDragBySelection);
                Assert.False(editor.EnableBlankLeftDragCanvas);
            });
        }

        [Fact]
        public void InfiniteCanvas_ZoomKeepsControlPointAnchored()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor
                {
                    LimitCanvasToContentBounds = false
                };
                editor.MoveCanvas(120f, -80f, bAnimation: false, CanvasMoveArgs.All);
                const float anchorX = 320f;
                const float anchorY = 240f;
                float canvasX = (anchorX - editor.CanvasOffsetX) / editor.CanvasScale;
                float canvasY = (anchorY - editor.CanvasOffsetY) / editor.CanvasScale;

                editor.ScaleCanvas(2f, anchorX, anchorY);

                Assert.Equal(2f, editor.CanvasScale);
                Assert.Equal(anchorX, canvasX * editor.CanvasScale + editor.CanvasOffsetX, 3);
                Assert.Equal(anchorY, canvasY * editor.CanvasScale + editor.CanvasOffsetY, 3);
            });
        }

        [Fact]
        public void InfiniteCanvas_ZoomStillUsesSafetyLimits()
        {
            RunInSta(() =>
            {
                using var editor = new STNodeEditor
                {
                    LimitCanvasToContentBounds = false
                };

                editor.ScaleCanvas(10f, 0f, 0f);
                Assert.Equal(5f, editor.CanvasScale);

                editor.ScaleCanvas(0.01f, 0f, 0f);
                Assert.Equal(0.2f, editor.CanvasScale);
            });
        }

        [Fact]
        public void FitCanvasToNodes_CentersTheContent()
        {
            RunInSta(() =>
            {
                using var editor = CreateEditorWithNode();
                editor.LimitCanvasToContentBounds = false;

                editor.MoveCanvas(5000f, -5000f, bAnimation: false, CanvasMoveArgs.All);
                editor.FitCanvasToNodes();

                float viewportCenterX = editor.ClientSize.Width / 2f;
                float viewportCenterY = editor.ClientSize.Height / 2f;
                float contentCenterX = editor.CanvasValidBounds.Left + editor.CanvasValidBounds.Width / 2f;
                float contentCenterY = editor.CanvasValidBounds.Top + editor.CanvasValidBounds.Height / 2f;
                Assert.Equal(viewportCenterX, contentCenterX * editor.CanvasScale + editor.CanvasOffsetX, 3);
                Assert.Equal(viewportCenterY, contentCenterY * editor.CanvasScale + editor.CanvasOffsetY, 3);
                Assert.InRange(editor.CanvasScale, 0.2f, 1f);
            });
        }

        [Fact]
        public void FitCanvasToNodes_CanLeaveAdditionalViewportMargin()
        {
            RunInSta(() =>
            {
                using var editor = CreateEditorWithNode();
                editor.LimitCanvasToContentBounds = false;

                editor.FitCanvasToNodes(0.85f);

                Assert.Equal(0.85f, editor.CanvasScale);
            });
        }

        [Fact]
        public void FlowEditorResize_PreservesFittedCanvasCenterAndScale()
        {
            RunInSta(() =>
            {
                using var canvas = new FlowEditorCanvas();
                canvas.Measure(new System.Windows.Size(1600, 900));
                canvas.Arrange(new System.Windows.Rect(0, 0, 1600, 900));
                canvas.UpdateLayout();

                STNodeEditor editor = canvas.NodeEditor;
                var node = new STNodeHub();
                node.Create();
                node.Left = 100;
                node.Top = 100;
                editor.Nodes.Add(node);
                editor.FitCanvasToNodes(0.85f);
                float fittedScale = editor.CanvasScale;

                canvas.Measure(new System.Windows.Size(800, 450));
                canvas.Arrange(new System.Windows.Rect(0, 0, 800, 450));
                canvas.UpdateLayout();

                float contentCenterX = editor.CanvasValidBounds.Left + editor.CanvasValidBounds.Width / 2f;
                float contentCenterY = editor.CanvasValidBounds.Top + editor.CanvasValidBounds.Height / 2f;
                Assert.Equal(fittedScale, editor.CanvasScale);
                Assert.Equal(400f, contentCenterX * editor.CanvasScale + editor.CanvasOffsetX, 3);
                Assert.Equal(225f, contentCenterY * editor.CanvasScale + editor.CanvasOffsetY, 3);
            });
        }

        [Fact]
        public void FlowEditorInitialLoad_FitsCanvasAfterFirstLayout()
        {
            RunInSta(() =>
            {
                using var canvas = new FlowEditorCanvas();
                STNodeEditor editor = canvas.NodeEditor;
                var node = new STNodeHub();
                node.Create();
                node.Left = 100;
                node.Top = 100;
                editor.Nodes.Add(node);
                editor.MoveCanvas(5000f, -5000f, bAnimation: false, CanvasMoveArgs.All);

                canvas.FitCanvasToNodesAfterLayout();
                canvas.Measure(new System.Windows.Size(800, 450));
                canvas.Arrange(new System.Windows.Rect(0, 0, 800, 450));
                canvas.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(
                    DispatcherPriority.Background,
                    new Action(() => { }));

                float contentCenterX = editor.CanvasValidBounds.Left + editor.CanvasValidBounds.Width / 2f;
                float contentCenterY = editor.CanvasValidBounds.Top + editor.CanvasValidBounds.Height / 2f;
                Assert.Equal(400f, contentCenterX * editor.CanvasScale + editor.CanvasOffsetX, 3);
                Assert.Equal(225f, contentCenterY * editor.CanvasScale + editor.CanvasOffsetY, 3);
                Assert.InRange(editor.CanvasScale, 0.2f, 0.85f);
            });
        }

        [Fact]
        public void DrawNodes_SkipsNodesOutsideTheViewport()
        {
            RunInSta(() =>
            {
                using var editor = new TestNodeEditor
                {
                    ShowBorder = false
                };
                var node = new TrackingNode();
                node.Create();
                node.Left = 2000;
                node.Top = 2000;
                editor.Nodes.Add(node);

                editor.DrawNodes(new System.Drawing.Rectangle(0, 0, 800, 600));
                Assert.Equal(0, node.DrawCount);

                node.Left = 100;
                node.Top = 100;
                editor.DrawNodes(new System.Drawing.Rectangle(0, 0, 800, 600));
                Assert.Equal(1, node.DrawCount);
            });
        }

        [Fact]
        public void RoundedNodes_KeepLegacyDefaultAndClipTheVisualCorners()
        {
            RunInSta(() =>
            {
                using var legacyEditor = new STNodeEditor();
                Assert.Equal(0, legacyEditor.NodeCornerRadius);
                Assert.True(legacyEditor.ShowNodeShadow);

                using var editor = new TestNodeEditor
                {
                    ShowBorder = false,
                    NodeCornerRadius = 8
                };
                var node = new TrackingNode();
                node.Create();
                node.Left = 20;
                node.Top = 20;
                editor.Nodes.Add(node);

                using var bitmap = editor.RenderNodes(new System.Drawing.Rectangle(0, 0, 240, 160));

                Assert.Equal(0, bitmap.GetPixel(node.Left, node.Top).A);
                Assert.True(bitmap.GetPixel(node.Left + node.Width / 2, node.Top + 1).A > 0);
                Assert.True(bitmap.GetPixel(node.Left + node.Width / 2, node.Bottom - 1).A > 0);
            });
        }

        [Fact]
        public void NodeTitleText_IsOffsetDownByTwoPixels()
        {
            RunInSta(() =>
            {
                var node = new TrackingNode();
                node.Create();

                System.Drawing.Rectangle titleRectangle = node.TitleRectangle;
                System.Drawing.Rectangle textRectangle = node.TitleTextRectangle;

                Assert.Equal(titleRectangle.X, textRectangle.X);
                Assert.Equal(titleRectangle.Y + 2, textRectangle.Y);
                Assert.Equal(titleRectangle.Size, textRectangle.Size);
            });
        }

        [Fact]
        public void NodeTitleProgress_FillsOnlyTheRequestedWidth()
        {
            RunInSta(() =>
            {
                using var editor = new TestNodeEditor
                {
                    ShowNodeShadow = false
                };
                var node = new TrackingNode
                {
                    Title = string.Empty,
                    TitleColor = System.Drawing.Color.Blue,
                    TitleProgressColor = System.Drawing.Color.Red,
                    TitleProgress = 0.5f
                };
                node.Create();
                node.Left = 20;
                node.Top = 20;
                editor.Nodes.Add(node);

                using var bitmap = editor.RenderNodes(new System.Drawing.Rectangle(0, 0, 240, 160));
                int sampleY = node.Top + node.TitleHeight / 2;

                Assert.Equal(System.Drawing.Color.Red.ToArgb(), bitmap.GetPixel(node.Left + node.Width / 4, sampleY).ToArgb());
                Assert.Equal(System.Drawing.Color.Blue.ToArgb(), bitmap.GetPixel(node.Left + node.Width * 3 / 4, sampleY).ToArgb());

                node.TitleProgress = 2f;
                Assert.Equal(1f, node.TitleProgress);
                node.TitleProgress = -0.5f;
                Assert.Equal(-1f, node.TitleProgress);
            });
        }

        [Fact]
        public void ShadowlessNodes_DrawNoNormalOuterGlowButKeepSelectionOutline()
        {
            RunInSta(() =>
            {
                using var editor = new TestNodeEditor
                {
                    ShowNodeShadow = false,
                    NodeCornerRadius = 10
                };
                var node = new TrackingNode();
                node.Create();
                node.Left = 20;
                node.Top = 20;
                editor.Nodes.Add(node);

                using var normalBitmap = editor.RenderNodes(new System.Drawing.Rectangle(0, 0, 240, 160));
                Assert.Equal(0, normalBitmap.GetPixel(node.Left - 2, node.Top + node.Height / 2).A);
                int normalEdgeColor = normalBitmap.GetPixel(node.Left, node.Top + node.Height / 2).ToArgb();

                node.IsSelected = true;
                using var selectedBitmap = editor.RenderNodes(new System.Drawing.Rectangle(0, 0, 240, 160));
                int selectedEdgeColor = selectedBitmap.GetPixel(node.Left, node.Top + node.Height / 2).ToArgb();
                Assert.NotEqual(normalEdgeColor, selectedEdgeColor);
                long selectedOutsideAlpha = SumAlpha(
                    selectedBitmap,
                    node.Left - 6,
                    node.Top + 10,
                    6,
                    node.Height - 20);
                Assert.Equal(0, selectedOutsideAlpha);

                editor.SetActiveNode(node);
                using var activeBitmap = editor.RenderNodes(new System.Drawing.Rectangle(0, 0, 240, 160));
                int activeEdgeColor = activeBitmap.GetPixel(node.Left, node.Top + node.Height / 2).ToArgb();
                long activeOutsideAlpha = SumAlpha(
                    activeBitmap,
                    node.Left - 6,
                    node.Top + 10,
                    6,
                    node.Height - 20);
                Assert.NotEqual(selectedEdgeColor, activeEdgeColor);
                Assert.Equal(0, activeOutsideAlpha);
            });
        }

        [Fact]
        public void GridOriginHighlight_CanBeDisabledWithoutRemovingTheGridLine()
        {
            RunInSta(() =>
            {
                using var legacyEditor = new STNodeEditor();
                Assert.True(legacyEditor.HighlightGridOrigin);

                using var editor = new TestNodeEditor
                {
                    BackColor = System.Drawing.Color.White,
                    GridColor = System.Drawing.Color.Black,
                    HighlightGridOrigin = false
                };

                using var bitmap = editor.RenderGrid(new System.Drawing.Size(140, 40));

                int originGridColor = bitmap.GetPixel(10, 15).ToArgb();
                int nextMajorGridColor = bitmap.GetPixel(110, 15).ToArgb();
                Assert.NotEqual(System.Drawing.Color.White.ToArgb(), originGridColor);
                Assert.Equal(nextMajorGridColor, originGridColor);
            });
        }

        private static STNodeEditor CreateEditorWithNode()
        {
            var editor = new STNodeEditor
            {
                ClientSize = new System.Drawing.Size(800, 600)
            };
            var node = new STNodeHub();
            node.Create();
            node.Left = 100;
            node.Top = 100;
            editor.Nodes.Add(node);
            return editor;
        }

        private static long SumAlpha(System.Drawing.Bitmap bitmap, int left, int top, int width, int height)
        {
            long alpha = 0;
            for (int y = top; y < top + height; y++)
            {
                for (int x = left; x < left + width; x++)
                {
                    alpha += bitmap.GetPixel(x, y).A;
                }
            }
            return alpha;
        }

        private static Window ShowLoaded(STNodeEditor editor)
        {
            var window = new Window
            {
                Content = editor,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Width = 1,
                Height = 1,
                Left = -10000,
                Top = -10000
            };
            window.Show();
            Assert.True(editor.IsLoaded);
            return window;
        }

        private static DispatcherTimer GetAnimationTimer(STNodeEditor editor)
        {
            return Assert.IsType<DispatcherTimer>(
                typeof(STNodeEditor)
                    .GetField("m_animation_timer", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(editor));
        }

        private static void AdvanceAnimationUntilIdle(STNodeEditor editor)
        {
            DispatcherTimer timer = GetAnimationTimer(editor);
            for (int i = 0; i < 200 && timer.IsEnabled; i++)
                AdvanceAnimation(editor);
            Assert.False(timer.IsEnabled);
        }

        private static void AdvanceAnimation(STNodeEditor editor)
        {
            typeof(STNodeEditor)
                .GetMethod("AnimationTimer_Tick", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(editor, [editor, EventArgs.Empty]);
        }

        private static void RunInSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception != null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }

        private sealed class TestNodeEditor : STNodeEditor
        {
            public static System.Drawing.Drawing2D.GraphicsPath CreateBezierPathForTest(
                float x1,
                float y1,
                float x2,
                float y2,
                float curvature)
            {
                return CreateBezierPath(x1, y1, x2, y2, curvature);
            }

            public static bool ShouldActivateNodeFromMouseForTest(STMouseButtons button)
            {
                return ShouldActivateNodeFromMouse(button);
            }

            public static bool ShouldPanBlankCanvasForTest(
                STMouseButtons button,
                bool enableBlankLeftDragCanvasAtMouseDown,
                System.Windows.Input.ModifierKeys modifiers)
            {
                return ShouldPanBlankCanvas(button, enableBlankLeftDragCanvasAtMouseDown, modifiers);
            }

            public static bool ShouldSelectNodeFromRectangleForTest(
                bool intersectsSelectionRectangle,
                bool wasSelectedBeforeDrag)
            {
                return ShouldSelectNodeFromRectangle(intersectsSelectionRectangle, wasSelectedBeforeDrag);
            }

            public void DrawNodes(System.Drawing.Rectangle viewport)
            {
                using var bitmap = RenderNodes(viewport);
            }

            public System.Drawing.Bitmap RenderNodes(System.Drawing.Rectangle viewport)
            {
                var bitmap = new System.Drawing.Bitmap(viewport.Width, viewport.Height);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                using var pen = new System.Drawing.Pen(System.Drawing.Color.Black);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
                OnDrawNode(new DrawingTools
                {
                    Graphics = graphics,
                    Pen = pen,
                    SolidBrush = brush
                }, viewport);
                return bitmap;
            }

            public System.Drawing.Bitmap RenderGrid(System.Drawing.Size size)
            {
                var bitmap = new System.Drawing.Bitmap(size.Width, size.Height);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                using var pen = new System.Drawing.Pen(System.Drawing.Color.Black);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
                graphics.Clear(BackColor);
                OnDrawGrid(new DrawingTools
                {
                    Graphics = graphics,
                    Pen = pen,
                    SolidBrush = brush
                }, size.Width, size.Height);
                return bitmap;
            }
        }

        private sealed class TrackingNode : STNode
        {
            public int DrawCount { get; private set; }
            public System.Drawing.Rectangle TitleTextRectangle => GetTitleTextRectangle();

            protected override void OnDrawNode(DrawingTools dt)
            {
                DrawCount++;
                base.OnDrawNode(dt);
            }
        }
    }
}

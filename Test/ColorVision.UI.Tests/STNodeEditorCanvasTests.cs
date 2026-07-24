#pragma warning disable CA1707
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests
{
    public class STNodeEditorCanvasTests
    {
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

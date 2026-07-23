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
                using var bitmap = new System.Drawing.Bitmap(viewport.Width, viewport.Height);
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                using var pen = new System.Drawing.Pen(System.Drawing.Color.Black);
                using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
                OnDrawNode(new DrawingTools
                {
                    Graphics = graphics,
                    Pen = pen,
                    SolidBrush = brush
                }, viewport);
            }
        }

        private sealed class TrackingNode : STNode
        {
            public int DrawCount { get; private set; }

            protected override void OnDrawNode(DrawingTools dt)
            {
                DrawCount++;
                base.OnDrawNode(dt);
            }
        }
    }
}

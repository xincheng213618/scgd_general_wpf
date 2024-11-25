using ColorVision.UI.Views;
using ScottPlot;
using ST.Library.UI.NodeEditor;
using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Flow
{
    /// <summary>
    /// CVFlowView.xaml 的交互逻辑
    /// </summary>
    public partial class CVFlowView1 : UserControl,IView
    {
        private FlowEngineLib.FlowEngineControl flowEngine;
        public FlowEngineLib.FlowEngineControl FlowEngineControl { get { return flowEngine; } set { flowEngine = value; } }
        public View View { get; set; }

        public CVFlowView1()
        {
            flowEngine = new FlowEngineLib.FlowEngineControl(false);
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            STNodePropertyGrid1.IsEditEnable = false;
            STNodeEditorMain.ActiveChanged += (s, e) =>
            {
                winf2.Visibility = STNodeEditorMain.ActiveNode == null ? Visibility.Collapsed : Visibility.Visible;
                STNodePropertyGrid1.SetNode(STNodeEditorMain.ActiveNode);
            };
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            View = new View();
            ViewGridManager.GetInstance().AddView(0, this);

            View.ViewIndexChangedEvent += (s, e) =>
            {
                if (e == -2)
                {
                    STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                    STNodeEditorMain.ContextMenuStrip.Items.Add("还原到主窗口中", null, (s, e1) =>
                    {

                        if (ViewGridManager.GetInstance().IsGridEmpty(View.PreViewIndex))
                        {
                            View.ViewIndex = View.PreViewIndex;
                        }
                        else
                        {
                            View.ViewIndex = -1;
                        }
                    }

                    );
                }
                else
                {
                    STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                    STNodeEditorMain.ContextMenuStrip.Items.Add("设为主窗口", null, (s, e1) => ViewGridManager.GetInstance().SetOneView(this));
                    STNodeEditorMain.ContextMenuStrip.Items.Add("显示全部窗口", null, (s, e1) => ViewGridManager.GetInstance().SetViewNum(-1));
                    STNodeEditorMain.ContextMenuStrip.Items.Add("独立窗口中显示", null, (s, e1) => View.ViewIndex = -2);
                }
            };
        }

        public float CanvasScale { get; set; }

        public void AutoSize()
        {
            // Calculate the centers
            var boundsCenterX = STNodeEditorMain.Bounds.Width / 2;
            var boundsCenterY = STNodeEditorMain.Bounds.Height / 2;

            // Calculate the scale factor to fit CanvasValidBounds within Bounds
            var scaleX = (float)STNodeEditorMain.Bounds.Width / (float)STNodeEditorMain.CanvasValidBounds.Width;
            var scaleY = (float)STNodeEditorMain.Bounds.Height / (float)STNodeEditorMain.CanvasValidBounds.Height;
            CanvasScale = Math.Min(scaleX, scaleY);
            CanvasScale = CanvasScale>1?1:CanvasScale;
            // Apply the scale
            STNodeEditorMain.ScaleCanvas(CanvasScale, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);

            var validBoundsCenterX = STNodeEditorMain.CanvasValidBounds.Width / 2;
            var validBoundsCenterY = STNodeEditorMain.CanvasValidBounds.Height / 2;

            // Calculate the offsets to move CanvasValidBounds to the center of Bounds
            var offsetX = boundsCenterX - validBoundsCenterX * CanvasScale - 50 * CanvasScale;
            var offsetY = boundsCenterY - validBoundsCenterY * CanvasScale - 50 * CanvasScale;


            // Move the canvas
            STNodeEditorMain.MoveCanvas(offsetX, STNodeEditorMain.CanvasOffset.Y, bAnimation: true, CanvasMoveArgs.Left);
            STNodeEditorMain.MoveCanvas(offsetX, offsetY, bAnimation: true, CanvasMoveArgs.Top);
        }



        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualWidth > 200)
            {
                winf1.Height = (int)ActualHeight;
                winf1.Width = (int)ActualWidth;
            }
        }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX + 100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                e.Handled = true;
            }
            else if (e.Key == Key.Right)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX - 100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY + 100, bAnimation: true, CanvasMoveArgs.Top);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY - 100, bAnimation: true, CanvasMoveArgs.Top);
                e.Handled = true;
            }
            else if (e.Key == Key.Add)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
                e.Handled = true;
            }
            else if (e.Key == Key.Subtract)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
                e.Handled = true;
            }
        }

    }
}

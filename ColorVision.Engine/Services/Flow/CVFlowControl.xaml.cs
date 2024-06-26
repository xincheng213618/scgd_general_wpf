using ColorVision.UI.Views;
using ST.Library.UI.NodeEditor;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Flow
{


    /// <summary>
    /// CVFlowView.xaml 的交互逻辑
    /// </summary>
    public partial class CVFlowView : UserControl,IView, IFlowView
    {
        private FlowEngineLib.FlowEngineControl flowEngine;
        public FlowEngineLib.FlowEngineControl FlowEngineControl { get { return flowEngine; }  set { flowEngine = value; } }
        public View View { get; set; }

        public CVFlowView()
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
                    if (!Grid1.Children.Contains(winf1))
                    {
                        Grid1.Children.Remove(airspace1);
                        airspace1.Content = null;
                        Grid1.Children.Add(winf1);
                    }

                    if (!Grid2.Children.Contains(winf2))
                    {
                        Grid2.Children.Remove(airspace2);
                        airspace2.Content = null;
                        Grid2.Children.Add(winf2);
                    }

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
                    if (!Grid1.Children.Contains(airspace1))
                    {
                        Grid1.Children.Remove(winf1);
                        airspace1.Content = winf1;
                        Grid1.Children.Add(airspace1);
                    }

                    if (!Grid2.Children.Contains(airspace2))
                    {
                        Grid2.Children.Remove(winf2);
                        airspace2.Content = winf2;
                        Grid2.Children.Add(airspace2);
                    }

                    STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
                    STNodeEditorMain.ContextMenuStrip.Items.Add("设为主窗口", null, (s, e1) => ViewGridManager.GetInstance().SetOneView(this));
                    STNodeEditorMain.ContextMenuStrip.Items.Add("显示全部窗口", null, (s, e1) => ViewGridManager.GetInstance().SetViewNum(-1));
                    STNodeEditorMain.ContextMenuStrip.Items.Add("独立窗口中显示", null, (s, e1) => View.ViewIndex = -2);
                }
            };

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

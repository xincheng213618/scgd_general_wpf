using FlowEngineLib;
using System.Windows.Controls;

namespace ColorVision.Flow
{
    /// <summary>
    /// FlowView.xaml 的交互逻辑
    /// </summary>
    public partial class FlowView : UserControl, IView
    {
        //private FlowEngineControl flowEngine;
        public FlowEngineControl FlowEngineControl { get { return flowEngine.FlowEngineControl; } }

        public View View { get; set; }

        public FlowView()
        {
            View = new View();
            //flowEngine = new FlowEngineControl(false);
            InitializeComponent();
        }

        //private void UserControl_Initialized(object sender, EventArgs e)
        //{
        //    STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
        //    STNodePropertyGrid1.IsEditEnable = false;
        //    STNodeEditorMain.ActiveChanged += (s, e) => 
        //    {
        //        winf2.Visibility = STNodeEditorMain.ActiveNode == null ?Visibility.Collapsed: Visibility.Visible;
        //        STNodePropertyGrid1.SetNode(STNodeEditorMain.ActiveNode);
        //    };
        //    flowEngine.AttachLoader(STNodeEditorMain);


        //    View.ViewIndexChangedEvent += (s, e) =>
        //    {
        //        if (e == -2)
        //        {
        //            STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
        //            STNodeEditorMain.ContextMenuStrip.Items.Add("还原到主窗口中", null, (s, e1) =>
        //            {

        //                if (ViewGridManager.GetInstance().IsGridEmpty(View.PreViewIndex))
        //                {
        //                    View.ViewIndex = View.PreViewIndex;
        //                }
        //                else
        //                {
        //                    View.ViewIndex = -1;
        //                }
        //            }

        //            );
        //        }
        //        else
        //        {
        //            STNodeEditorMain.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
        //            STNodeEditorMain.ContextMenuStrip.Items.Add("设为主窗口", null, (s, e1) => ViewGridManager.GetInstance().SetOneView(this));
        //            STNodeEditorMain.ContextMenuStrip.Items.Add("显示全部窗口", null, (s, e1) => ViewGridManager.GetInstance().SetViewNum(-1));
        //            STNodeEditorMain.ContextMenuStrip.Items.Add("独立窗口中显示", null, (s, e1) => View.ViewIndex = -2);
        //        }
        //    };

        //}

        //private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        //{
        //    if (this.ActualWidth > 200)
        //    {
        //        winf1.Height = (int)this.ActualHeight;
        //        winf1.Width = (int)this.ActualWidth;
        //    }
        //}

        //private void UserControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        //{
        //    if (e.Key == Key.Left)
        //    {
        //        STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX +100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
        //        e.Handled = true;
        //    }
        //    else if (e.Key == Key.Right)
        //    {
        //        STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX- 100, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
        //        e.Handled = true;
        //    }
        //    else if (e.Key == Key.Up)
        //    {
        //        STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY + 100, bAnimation: true, CanvasMoveArgs.Top);
        //        e.Handled = true;
        //    }
        //    else if (e.Key == Key.Down)
        //    {
        //        STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY - 100, bAnimation: true, CanvasMoveArgs.Top);
        //        e.Handled = true;
        //    }
        //    else if (e.Key == Key.Add)
        //    {
        //        STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
        //        e.Handled = true;
        //    }
        //    else if (e.Key == Key.Subtract)
        //    {
        //        STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, (STNodeEditorMain.Width / 2), (STNodeEditorMain.Height / 2));
        //        e.Handled = true;
        //    }
        //}
    }
}

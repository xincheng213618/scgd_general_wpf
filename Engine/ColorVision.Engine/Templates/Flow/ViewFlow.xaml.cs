#pragma warning disable CA1720,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.ImageEditor;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Flow
{
    /// <summary>
    /// CVFlowView.xaml 的交互逻辑
    /// </summary>
    public partial class ViewFlow : UserControl,IView,IDisposable
    {
        public  FlowEngineControl FlowEngineControl { get; set; }

        public static FlowEngineManager FlowEngineManager => FlowEngineManager.GetInstance();

        public View View { get; set; }
        public RelayCommand AutoSizeCommand { get; set; }

        public event EventHandler RefreshFlow;

        public RelayCommand RefreshCommand { get; set; }

        public RelayCommand ClearCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public RelayCommand AutoAlignmentCommand { get; set; }

        public RelayCommand OpenFlowTemplateCommand { get; set; }

        public static FlowEngineConfig Config => FlowEngineConfig.Instance;


        public DisplayFlow DisplayFlow { get; set; }

        public ViewFlow(FlowEngineControl flowEngineControl)
        {
            FlowEngineControl = flowEngineControl;
            InitializeComponent();
            AutoSizeCommand = new RelayCommand(a => AutoSize());
            RefreshCommand = new RelayCommand(a => Refresh());
            ClearCommand = new RelayCommand(a => Clear());
            SaveCommand = new RelayCommand(a => Save());
            AutoAlignmentCommand = new RelayCommand(a => AutoAlignment());
            OpenFlowTemplateCommand = new RelayCommand(a => OpenFlowTemplate());

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => Save(), (s, e) => { e.CanExecute = STNodeEditorHelper.CheckFlow(); }));

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => Clear(), (s, e) => { e.CanExecute = true; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Clear(), (s, e) => { e.CanExecute = true; }));

            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo(),(s,e) => { e.CanExecute = UndoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo(), (s, e) => { e.CanExecute = RedoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(Commands.UndoHistory, null, (s, e) => { e.CanExecute = UndoStack.Count > 0; if (e.Parameter is MenuItem m1 && m1.ItemsSource != UndoStack) m1.ItemsSource = UndoStack; }));
            CommandBindings.Add(new CommandBinding(EngineCommands.StartExecutionCommand, (s, e) => DisplayFlow.RunFlow(), (s, e) =>
            {
                if (DisplayFlow.flowControl != null)
                    e.CanExecute = !DisplayFlow.flowControl.IsFlowRun;
            }));
            CommandBindings.Add(new CommandBinding(EngineCommands.StopExecutionCommand, (s, e) => DisplayFlow.StopFlow(), (s, e) =>
            {
                if (DisplayFlow.flowControl != null)
                    e.CanExecute = DisplayFlow.flowControl.IsFlowRun;
            }));

            if (Config.UseNewUI)
            {
                ThemeManager.Current.CurrentUIThemeChanged += ThemeChanged;
                ThemeChanged(ThemeManager.Current.CurrentUITheme);
            }



        }

        void ThemeChanged(Theme theme)
        {
            if (theme == Theme.Dark)
            {
                STNodeEditorMain.BackColor = Color.FromArgb(255, 34, 34, 34);
                STNodeEditorMain.GridColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.ForeColor = Color.FromArgb(255, 255, 255, 255);
                STNodeEditorMain.LocationBackColor = Color.FromArgb(255, 50, 50, 50);



            }
            else
            {
                STNodeEditorMain.BackColor = Color.FromArgb(255, 150, 150, 150);
                STNodeEditorMain.GridColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.ForeColor = Color.FromArgb(255, 0, 0, 0);
                STNodeEditorMain.LocationBackColor = Color.FromArgb(255, 200, 200, 200);
            }
        }
        #region ActionCommand

        public ObservableCollection<ActionCommand> UndoStack { get; set; } = new ObservableCollection<ActionCommand>();
        public ObservableCollection<ActionCommand> RedoStack { get; set; } = new ObservableCollection<ActionCommand>();

        public void ClearActionCommand()
        {
            UndoStack.Clear();
            RedoStack.Clear();
        }

        public void AddActionCommand(ActionCommand actionCommand)
        {
            UndoStack.Add(actionCommand);
            RedoStack.Clear();
        }

        public void Undo()
        {
            if (UndoStack.Count > 0)
            {
                var undoAction = UndoStack[^1]; // Access the last element
                UndoStack.RemoveAt(UndoStack.Count - 1); // Remove the last element
                undoAction.UndoAction();
                RedoStack.Add(undoAction);
            }
        }

        public void Redo()
        {
            if (RedoStack.Count > 0)
            {
                var redoAction = RedoStack[^1]; // Access the last element
                RedoStack.RemoveAt(RedoStack.Count - 1); // Remove the last element
                redoAction.RedoAction();
                UndoStack.Add(redoAction);
            }
        }
        #endregion

        public void OpenFlowTemplate()
        {
            new TemplateEditorWindow(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            Refresh();
        }

        public void AutoAlignment()
        {
            STNodeEditorHelper.ApplyTreeLayout(startX: 100, startY: 100, horizontalSpacing: 250, verticalSpacing: 200);
            STNodeEditorHelper.AutoSize();
        }

        public void Save()
        {
            if (!STNodeEditorHelper.CheckFlow()) return;
            FlowParam.DataBase64 = Convert.ToBase64String(STNodeEditorMain.GetCanvasData());
            TemplateFlow.Save2DB(FlowParam);
            MessageBox.Show(Application.Current.GetActiveWindow(),"保存成功","Flow");
        }


        public void Refresh()
        {
            RefreshFlow?.Invoke(this, new EventArgs());
        }
        public void Clear()
        {
            STNodeEditorMain.Nodes.Clear();
        }

        public STNodeEditorHelper STNodeEditorHelper { get; set; }

        public FlowParam FlowParam { get; set; }

        public float CanvasScale { get => STNodeEditorHelper.CanvasScale; set { STNodeEditorMain.ScaleCanvas(value, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2); } }


        STNodeTreeView STNodeTreeView1 = new STNodeTreeView();
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = this;
            STNodeTreeView1.LoadAssembly("FlowEngineLib.dll");
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            STNodeEditorMain.ActiveChanged +=(s,e) => SignStackBorder.Visibility = STNodeEditorMain.ActiveNode != null ? Visibility.Visible : Visibility.Collapsed;
            STNodeEditorMain.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.Delete)
                {
                    if (STNodeEditorMain.ActiveNode != null)
                    {
                        var node = STNodeEditorMain.ActiveNode;
                        STNodeEditorMain.Nodes.Remove(node);
                    }

                    foreach (var item in STNodeEditorMain.GetSelectedNode())
                    {
                        STNodeEditorMain.Nodes.Remove(item);
                    }
                }
                if (e.KeyCode == System.Windows.Forms.Keys.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    AutoAlignment();
                }
            };

            FlowEngineControl.AttachNodeEditor(STNodeEditorMain);


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
            };
            STNodeEditorHelper = new STNodeEditorHelper(this,STNodeEditorMain, STNodeTreeView1, STNodePropertyGrid1, SignStackPannel);
        }



        // 渲染 STNodeEditorMain 到 BitmapSource
        public BitmapSource RenderMiniMap()
        {
            // 获取 WinForms 控件的句柄并生成 Bitmap
            var bmp = new System.Drawing.Bitmap(STNodeEditorMain.Width, STNodeEditorMain.Height);
            STNodeEditorMain.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height));

            // 转换为 WPF BitmapSource 并缩放
            var hBitmap = bmp.GetHbitmap();
            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(STNodeEditorMain.Width, STNodeEditorMain.Height)
            );
            // 释放 GDI 资源
            NativeMethods.DeleteObject(hBitmap);
            bmp.Dispose();
            return bitmapSource;
        }

        // Win32 资源释放
        internal static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("gdi32.dll")]
            public static extern bool DeleteObject(IntPtr hObject);
        }


        public void AutoSize()
        {
            STNodeEditorHelper.AutoSize();
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
            if (STNodeEditorMain.ActiveNode == null && STNodeEditorMain.GetSelectedNode().Length == 0)
            {
                if (e.Key == Key.Add)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);
                    e.Handled = true;
                }
                else if (e.Key == Key.Subtract)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);
                    e.Handled = true;
                }
            }
            else
            {

                foreach (var item in STNodeEditorMain.GetSelectedNode())
                {
                    if (e.Key == Key.Left)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X - 10, item.Location.Y);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X + 10, item.Location.Y);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X - 10, item.Location.Y);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Right)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X + 10, item.Location.Y);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X - 10, item.Location.Y);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X + 10, item.Location.Y);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Up)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y - 10);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y + 10);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y - 10);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Down)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y + 10);
                        Action undoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y - 10);
                        Action redoaction = () => item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y + 10);

                        ActionCommand actionCommand = new ActionCommand(undoaction, redoaction);
                        AddActionCommand(actionCommand);

                        e.Handled = true;
                    }
                }
            }
        }


        private bool IsMouseDown;
        private System.Drawing.Point lastMousePosition;
        private void STNodeEditorMain_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            lastMousePosition = e.Location;
            System.Drawing.PointF m_pt_down_in_canvas = new System.Drawing.PointF();
            m_pt_down_in_canvas.X = ((float)e.X - STNodeEditorMain.CanvasOffsetX) / STNodeEditorMain.CanvasScale;
            m_pt_down_in_canvas.Y = ((float)e.Y - STNodeEditorMain.CanvasOffsetY) / STNodeEditorMain.CanvasScale;
            NodeFindInfo nodeFindInfo = STNodeEditorMain.FindNodeFromPoint(m_pt_down_in_canvas);

            if (!string.IsNullOrEmpty(nodeFindInfo.Mark))
            {

            }
            else if (nodeFindInfo.Node != null)
            {

            }
            else if (nodeFindInfo.NodeOption != null)
            {

            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                IsMouseDown = true;
            }
        }

        private void STNodeEditorMain_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            IsMouseDown = false;
        }

        private void STNodeEditorMain_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if ( Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && IsMouseDown)
            {        // 计算鼠标移动的距离
                int deltaX = e.X - lastMousePosition.X;
                int deltaY = e.Y - lastMousePosition.Y;

                // 更新画布偏移
                STNodeEditorMain.MoveCanvas(
                    STNodeEditorMain.CanvasOffsetX + deltaX,
                    STNodeEditorMain.CanvasOffsetY + deltaY,
                    bAnimation: false,
                    CanvasMoveArgs.All
                );

                // 更新最后的鼠标位置
                lastMousePosition = e.Location;
            }
        }


        private void STNodeEditorMain_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var mousePosition = e.Location; // e.Location 已是控件坐标
            float delta = e.Delta > 0 ? 0.05f : -0.05f;
            STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + delta, mousePosition.X, mousePosition.Y);
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }


        public void Dispose()
        {
            ThemeManager.Current.CurrentUIThemeChanged -= ThemeChanged;

            STNodeEditorMain?.Dispose();
            STNodeTreeView1?.Dispose();
            winf1?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var Batch = BatchResultMasterDao.Instance.GetByCode(FlowEngineManager.GetInstance().CurrentFlowMsg.SerialNumber);
            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            Frame frame = new Frame();

            MeasureBatchPage batchDataHistory = new MeasureBatchPage(frame, Batch);

            Window window = new Window() { Owner = Application.Current.GetActiveWindow() };
            window.Content = batchDataHistory;
            window.Show();

        }

        private void Grid1_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Grid grid)
            {
                winf2.Visibility = grid.ActualHeight < 500 || grid.ActualWidth < 300 ? Visibility.Collapsed : Visibility.Visible;

                winf1.Visibility = grid.ActualHeight < 200 || grid.ActualWidth < 100 ? Visibility.Collapsed : Visibility.Visible;
            }
        }
    }
}

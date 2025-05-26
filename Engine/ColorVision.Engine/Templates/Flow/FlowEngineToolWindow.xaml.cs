using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Flow
{

    public class FileProcessorFlow : IFileProcessor
    {
        public string GetExtension() => "stn|*.stn"; // "cvcie
        public int Order => 1;

        public bool CanProcess(string filePath)
        {
            return filePath.EndsWith("stn", StringComparison.OrdinalIgnoreCase);
        }
        public void Export(string filePath)
        {
          
        }

        public bool CanExport(string filePath)
        {
            return false;
        }

        public void Process(string filePath)
        {
            FlowEngineToolWindow flowEngineToolWindow = new FlowEngineToolWindow
            {
                Owner = System.Windows.Application.Current.GetActiveWindow()
            };
            flowEngineToolWindow.OpenFlow(filePath);
            flowEngineToolWindow.Show();
        }
    }



    /// <summary>
    /// Interaction logic for MarkdownViewWindow.xaml
    /// </summary>
    public partial class FlowEngineToolWindow : Window,INotifyPropertyChanged
    {

        private static ILog log = LogManager.GetLogger(typeof(FlowEngineToolWindow));
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public FlowEngineToolWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (s, e) => Undo(), (s, e) => { e.CanExecute = UndoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (s, e) => Redo(), (s, e) => { e.CanExecute = RedoStack.Count > 0; }));
            this.CommandBindings.Add(new CommandBinding(Commands.UndoHistory, null, (s, e) => { e.CanExecute = UndoStack.Count > 0; if (e.Parameter is MenuItem m1 && m1.ItemsSource != UndoStack) m1.ItemsSource = UndoStack; }));
        }

        FlowParam FlowParam { get; set; }

        public STNodeEditorHelper STNodeEditorHelper { get; set; }

        public FlowEngineToolWindow(FlowParam flowParam) : this()
        {
            FlowParam = flowParam;
            OpenFlowBase64(flowParam);
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

        public float CanvasScale { get => STNodeEditorHelper.CanvasScale; set { STNodeEditorMain.ScaleCanvas(value, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2); NotifyPropertyChanged(); } }

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (STNodeEditorMain.ActiveNode == null && STNodeEditorMain.GetSelectedNode().Length ==0)
            {
                if (e.Key == Key.Left)
                {
                    STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX + 100*CanvasScale, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX - 100 * CanvasScale, STNodeEditorMain.CanvasOffsetY, bAnimation: true, CanvasMoveArgs.Left);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY + 100 * CanvasScale, bAnimation: true, CanvasMoveArgs.Top);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    STNodeEditorMain.MoveCanvas(STNodeEditorMain.CanvasOffsetX, STNodeEditorMain.CanvasOffsetY - 100 * CanvasScale, bAnimation: true, CanvasMoveArgs.Top);
                    e.Handled = true;
                }
                else if (e.Key == Key.Add)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.1f, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);
                    NotifyPropertyChanged(nameof(CanvasScale));
                    e.Handled = true;
                }
                else if (e.Key == Key.Subtract)
                {
                    STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.1f, STNodeEditorMain.CanvasValidBounds.X + STNodeEditorMain.CanvasValidBounds.Width / 2, STNodeEditorMain.CanvasValidBounds.Y + STNodeEditorMain.CanvasValidBounds.Height / 2);
                    NotifyPropertyChanged(nameof(CanvasScale));
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
                    }
                    else if (e.Key == Key.Right)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X + 10, item.Location.Y);
                    }
                    else if (e.Key == Key.Up)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y - 10);
                    }
                    else if (e.Key == Key.Down)
                    {
                        item.Location = new System.Drawing.Point(item.Location.X, item.Location.Y + 10);
                    }
                    if (e.Key == Key.Delete)
                    {
                        STNodeEditorMain.Nodes.Remove(item);
                    }
                }   
            }

        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            STNodePropertyGrid1.Text = "属性";
            STNodeTreeView1.LoadAssembly("FlowEngineLib.dll");

            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");

            STNodeEditorMain.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == System.Windows.Forms.Keys.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Save();
                }
                if (e.KeyCode == System.Windows.Forms.Keys.R && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Refresh();
                }
                if (e.KeyCode == System.Windows.Forms.Keys.L && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    AutoAlignment();
                }
            };
            this.DataContext = this;
            this.Closed += (s, e) =>
            {
                if (FlowConfig.Instance.IsAutoEditSave)
                {
                    if (AutoSave())
                    {
                        if (MessageBox.Show("是否保存修改", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            Save();
                        }
                    }
                }

            };

            STNodeEditorHelper = new STNodeEditorHelper(this,STNodeEditorMain, STNodeTreeView1,STNodePropertyGrid1, SignStackPannel);
        }
        public void AutoAlignment()
        {
            STNodeEditorHelper.ApplyTreeLayout(startX: 100, startY: 100, horizontalSpacing: 250, verticalSpacing: 200);
            STNodeEditorHelper.AutoSize();
        }
        public void Refresh()
        {
            OpenFlowBase64(FlowParam);
        }

        public void SaveToFile()
        {
            // 创建并配置 SaveFileDialog
            using System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            saveFileDialog.Filter = "CVFlow files (*.cvflow)|*.cvflow";
            saveFileDialog.DefaultExt = "cvflow";
            saveFileDialog.AddExtension = true;
            saveFileDialog.Title = "Save As";
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SaveToFile(saveFileDialog.FileName);
            }
        }

        public void SaveToFile(string filePath)
        {
            // 获取画布数据
            byte[] data = STNodeEditorMain.GetCanvasData();

            // 检查数据是否为空
            if (data == null || data.Length == 0)
            {
                Console.WriteLine("No data to save.");
                return;
            }

            try
            {
                // 将数据写入指定文件路径
                File.WriteAllBytes(filePath, data);
                Console.WriteLine("File saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while saving the file: {ex.Message}");
            }

        }


        private void Button_Click_New(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog ofd = new();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            Save();
            STNodeEditorMain.Nodes.Clear();
        }

        private void Button_Click_Save(object sender, RoutedEventArgs e)
        {
            Save();
        }
        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("您是否清空已经创建流程\n\r清空后自动保存关闭", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                STNodeEditorMain.Nodes.Clear();
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog ofd = new();
            ofd.Filter = "*.stn|*.stn";
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            ButtonSave.Visibility = Visibility.Visible;
            OpenFlow(ofd.FileName);
        }
        string FileFlow;
        public void OpenFlow(string flowName)
        {
            FileFlow = flowName;
            STNodeEditorMain.Nodes.Clear();
            STNodeEditorMain.LoadCanvas(flowName);
            Title = "流程编辑器 - " + new FileInfo(flowName).Name;
        }

        public void OpenFlowBase64(FlowParam flowParam)
        {
            STNodeEditorMain.Nodes.Clear();
            if (!string.IsNullOrEmpty(flowParam.DataBase64))
            {
                try
                {
                    STNodeEditorMain.LoadCanvas(Convert.FromBase64String(flowParam.DataBase64));
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                    MessageBox.Show(ex.Message);
                }

            }
            Title = "流程编辑器 - " + new FileInfo(flowParam.Name).Name;
        }



        private bool AutoSave()
        {
            if (FlowParam == null) return false;
            var data = STNodeEditorMain.GetCanvasData();

            string base64 = Convert.ToBase64String(data);
            return FlowParam.DataBase64.Length != base64.Length;
        }

        private void Save()
        {
            if (File.Exists(FileFlow))
            {
                MessageBox.Show("保存成功");
                SaveToFile(FileFlow);
                return;
            }
            else if (FlowParam !=null)
            {
                if (!STNodeEditorHelper.CheckFlow()) return;
                var data = STNodeEditorMain.GetCanvasData();
                FlowParam.DataBase64 = Convert.ToBase64String(data);
                FlowParam.Save2DB(FlowParam);
                MessageBox.Show("保存成功");
            }
        }


        private void AutoAlignment_Click(object sender, RoutedEventArgs e)
        {
            STNodeEditorHelper.ApplyTreeLayout(startX: 100, startY: 100, horizontalSpacing: 250, verticalSpacing: 200);
            STNodeEditorHelper.AutoSize();
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
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && IsMouseDown)
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
            var mousePosition = STNodeEditorMain.PointToClient(e.Location);

            if (e.Delta < 0)
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale - 0.05f, mousePosition.X, mousePosition.Y);
                NotifyPropertyChanged(nameof(CanvasScale));
            }
            else
            {
                STNodeEditorMain.ScaleCanvas(STNodeEditorMain.CanvasScale + 0.05f, mousePosition.X, mousePosition.Y);
                NotifyPropertyChanged(nameof(CanvasScale));
            }
        }



    }
}

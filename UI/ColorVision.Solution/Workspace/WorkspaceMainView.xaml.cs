using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// WorkspaceMainView.xaml 的交互逻辑
    /// AvalonDock 布局管理已提升到 MainWindow 级别。
    /// 此控件保留用于兼容性。
    /// </summary>
    public partial class WorkspaceMainView : UserControl
    {
        public WorkspaceMainView()
        {
            InitializeComponent();
            WorkspaceManager.SolutionView = this;
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (sender, e) => Closed()));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Close, new KeyGesture(Key.W, ModifierKeys.Control)));
        }

        public void Closed()
        {
            var pannel = WorkspaceManager.FindDocumentActive(WorkspaceManager.LayoutDocumentPane);
            pannel?.Close();
        }
    }
}

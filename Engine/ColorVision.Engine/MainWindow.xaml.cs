using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Views;
using HandyControl.Interactivity;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine
{
    /// <summary>
    /// Interaction logic for MarkdownViewWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        public IViewManager ViewManager { get; set; }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MenuManager.GetInstance().LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1);

            // Engine MainWindow 使用 ViewGridManager（无 AvalonDock）
            var gridManager = ViewGridManager.GetInstance();
            gridManager.MainView = ViewGrid;
            ViewManager = gridManager;
            ViewManagerProvider.Current = gridManager;

            ViewManager.SetViewGrid(ViewConfig.Instance.ViewMaxCount);
            ViewManager.ViewMaxChangedEvent += (e) => ViewConfig.Instance.ViewMaxCount = e;

            DisPlayManager.GetInstance().Init(this, StackPanelSPD);

            Debug.WriteLine(Properties.Resources.LaunchSuccess);
            this.LoadHotKeyFromAssembly();

            Application.Current.MainWindow = this;
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FluidMoveBehavior fluidMoveBehavior = new()
                    {
                        AppliesTo = FluidMoveScope.Children,
                        Duration = TimeSpan.FromSeconds(0.1)
                    };
                    Interaction.GetBehaviors(StackPanelSPD).Add(fluidMoveBehavior);
                });
            });

        }
    }
}
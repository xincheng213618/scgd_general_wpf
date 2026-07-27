using ColorVision.Themes;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using ColorVision.UI.Views;
using HandyControl.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine
{
    /// <summary>
    /// Interaction logic for MarkdownViewWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string AcquirePanelTitle = "Device Control";

        public MainWindow()
        {
            InitializeComponent();
            Title = "ColorVision Engine";
            this.ApplyCaption();
            ApplyDockTheme(ThemeManager.Current.CurrentUITheme);
            ThemeManager.Current.CurrentUIThemeChanged += ApplyDockTheme;
            Closed += (_, _) => ThemeManager.Current.CurrentUIThemeChanged -= ApplyDockTheme;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MenuManager.GetInstance().LoadMenuForWindow(MenuItemConstants.MainWindowTarget, Menu1, IsStandaloneMenuType);

            ConfigureWorkspace();
            RegisterStandaloneDockPanels();

            DisPlayManager.GetInstance().Init(this, StackPanelSPD);
            DockViewManager.GetInstance().ShowAllViews();

            Debug.WriteLine("Started successfully");
            this.LoadHotKeyFromAssembly();

            DockingManager1.ActiveContentChanged += (s, eArgs) =>
            {
                var viewManager = DockViewManager.GetInstance();
                var activeControl = DockingManager1.ActiveContent as Control;
                var activeView = activeControl != null && viewManager.Views.Contains(activeControl) ? activeControl : null;
                viewManager.RaiseActiveViewChanged(activeView);
            };

            Application.Current.MainWindow = this;
            Dispatcher.InvokeAsync(() =>
            {
                if (Interaction.GetBehaviors(StackPanelSPD).OfType<FluidMoveBehavior>().Any())
                {
                    return;
                }

                FluidMoveBehavior fluidMoveBehavior = new()
                {
                    AppliesTo = FluidMoveScope.Children,
                    Duration = TimeSpan.FromSeconds(0.1)
                };
                Interaction.GetBehaviors(StackPanelSPD).Add(fluidMoveBehavior);
            });

        }

        private void ConfigureWorkspace()
        {
            WorkspaceManager.layoutRoot = _layoutRoot;
            WorkspaceManager.LayoutDocumentPane = LayoutDocumentPane;

            var layoutManager = new DockLayoutManager(DockingManager1);
            layoutManager.RegisterPanel("AcquirePanel", ScrollViewerDisplay, AcquirePanelTitle, PanelPosition.Left);
            WorkspaceManager.LayoutManager = layoutManager;

            DockViewManagerHost.Initialize();
        }

        private static void RegisterStandaloneDockPanels()
        {
            var providerInstances = AssemblyHandler.GetInstance().GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => typeof(IDockPanelProvider).IsAssignableFrom(type)
                               && !type.IsAbstract
                               && !type.IsInterface
                               && !type.ContainsGenericParameters
                               && IsStandaloneDockPanelProvider(type))
                .Select(type =>
                {
                    try
                    {
                        return Activator.CreateInstance(type) as IDockPanelProvider;
                    }
                    catch
                    {
                        return null;
                    }
                })
                .Where(provider => provider != null)
                .OrderBy(provider => provider!.Order)
                .ToList();

            foreach (var provider in providerInstances)
            {
                try
                {
                    provider!.RegisterPanels();
                }
                catch
                {
                }
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null)!;
            }
        }

        private static bool IsStandaloneMenuType(Type type)
        {
            string? assemblyName = type.Assembly.GetName().Name;
            return !string.Equals(assemblyName, "ColorVision.Solution", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assemblyName, "ColorVision.UI.Desktop", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(assemblyName, "ColorVision.Copilot", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsStandaloneDockPanelProvider(Type type)
        {
            string? assemblyName = type.Assembly.GetName().Name;
            return string.Equals(assemblyName, "ColorVision.Engine", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyDockTheme(Theme theme)
        {
            DockingManager1.Theme = null;
            DockingManager1.Theme = theme == Theme.Dark
                ? new AvalonDock.Themes.Vs2013DarkTheme()
                : new AvalonDock.Themes.Vs2013LightTheme();

        }
    }
}

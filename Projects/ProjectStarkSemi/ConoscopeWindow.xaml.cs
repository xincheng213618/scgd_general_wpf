using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ProjectStarkSemi.Conoscope;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProjectStarkSemi
{
    public class ConoscopeWindowConfig : WindowConfig
    {
        public static ConoscopeWindowConfig Instance => ConfigService.Instance.GetRequiredService<ConoscopeWindowConfig>();
    }

    public partial class ConoscopeWindow : Window, IDisposable
    {
        public static ConoscopeWindow? Instance { get; private set; }

        private ThemeChangedHandler? themeChangedHandler;
        private bool isUpdatingModelSelection;
        private MVSViewWindow? observationCameraWindow;

        public ConoscopeWindow()
            : this(createInitialView: true)
        {
        }

        public ConoscopeWindow(string filePath)
            : this(createInitialView: false)
        {
            OpenConoscope(filePath);
        }

        private ConoscopeWindow(bool createInitialView)
        {
            InitializeComponent();
            Instance = this;
            Title = "Conoscope " + (Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? string.Empty);
            DataContext = ConoscopeManager.GetInstance();
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            MenuManager.GetInstance().LoadMenuForWindow("Conoscope", menu);
            InitializeTheme();
            InitializeModelSelector();

            ConoscopeManager.GetInstance().Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            ConoscopeManager.GetInstance().Config.ModelTypeChanged += ConoscopeConfig_ModelTypeChanged;
            RefreshWindowModelState();

            if (createInitialView)
            {
                AddConoscopeView(null, activate: true);
            }

            Closing += (s, e) => SaveAllViewLayouts();
            Closed += (s, e) =>
            {
                if (ReferenceEquals(Instance, this))
                {
                    Instance = null;
                }

                Dispose();
            };
        }

        public ConoscopeView? ActiveView => GetActiveView();

        internal ProjectStarkSemi.Layout.DockLayoutManager? LayoutManager => ActiveView?.LayoutManager;

        public void OpenConoscope(string filename)
        {
            AddConoscopeView(filename, activate: true);
        }

        internal void RefreshConoscopeConfiguration()
        {
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.RefreshConoscopeConfiguration();
            }

            RefreshWindowModelState();
        }

        private void InitializeTheme()
        {
            void ThemeChange(Theme theme)
            {
                DockingManager.Theme = theme == Theme.Dark
                    ? new AvalonDock.Themes.Vs2013DarkTheme()
                    : new AvalonDock.Themes.Vs2013LightTheme();
            }

            themeChangedHandler = ThemeChange;
            ThemeChange(ThemeManager.Current.CurrentUITheme);
            ThemeManager.Current.CurrentUIThemeChanged += themeChangedHandler;
        }

        private void InitializeModelSelector()
        {
            cbModelType.ItemsSource = Enum.GetValues(typeof(ConoscopeModelType));
            isUpdatingModelSelection = true;
            try
            {
                cbModelType.SelectedItem = ConoscopeManager.GetInstance().Config.CurrentModel;
            }
            finally
            {
                isUpdatingModelSelection = false;
            }
        }

        private void ConoscopeConfig_ModelTypeChanged(object? sender, ConoscopeModelType e)
        {
            RefreshWindowModelState();
        }

        private void RefreshWindowModelState()
        {
            ConoscopeConfig config = ConoscopeManager.GetInstance().Config;
            tbCurrentModel.Text = config.CurrentModelProfile.DisplayName;
            btnOpenObservationCamera.Visibility = config.CurrentModelProfile.HasObservationCamera
                ? Visibility.Visible
                : Visibility.Collapsed;

            isUpdatingModelSelection = true;
            try
            {
                cbModelType.SelectedItem = config.CurrentModel;
            }
            finally
            {
                isUpdatingModelSelection = false;
            }
        }

        private ConoscopeView AddConoscopeView(string? filePath, bool activate)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                string existingContentId = GetContentId(filePath);
                LayoutDocument? existingDocument = ViewDocumentPane.Children
                    .OfType<LayoutDocument>()
                    .FirstOrDefault(item => item.ContentId == existingContentId);
                if (existingDocument?.Content is ConoscopeView existingView)
                {
                    SelectDocument(existingDocument);
                    ConoscopeModuleService.Activate(existingView);
                    return existingView;
                }
            }

            ConoscopeView view = new ConoscopeView();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                view.OpenConoscope(filePath);
            }

            LayoutDocument layoutDocument = new LayoutDocument
            {
                Title = string.IsNullOrWhiteSpace(filePath) ? "Conoscope" : Path.GetFileName(filePath),
                ContentId = string.IsNullOrWhiteSpace(filePath) ? $"StandaloneConoscope:{Guid.NewGuid():N}" : GetContentId(filePath),
                Content = view,
                CanClose = true,
                CanFloat = true
            };

            layoutDocument.IsActiveChanged += (s, e) =>
            {
                if (layoutDocument.IsActive)
                {
                    ConoscopeModuleService.Activate(view);
                }
            };
            layoutDocument.Closing += (s, e) => view.Dispose();

            ViewDocumentPane.Children.Add(layoutDocument);
            if (activate)
            {
                SelectDocument(layoutDocument);
            }

            return view;
        }

        private void SelectDocument(LayoutDocument document)
        {
            ViewDocumentPane.SelectedContentIndex = ViewDocumentPane.IndexOf(document);
            document.IsActive = true;
            if (document.Content is ConoscopeView view)
            {
                ConoscopeModuleService.Activate(view);
            }
        }

        private ConoscopeView? GetActiveView()
        {
            LayoutDocument? activeDocument = ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .FirstOrDefault(item => item.IsActive);

            if (activeDocument?.Content is ConoscopeView activeView)
            {
                return activeView;
            }

            int selectedIndex = ViewDocumentPane.SelectedContentIndex;
            if (selectedIndex >= 0 && selectedIndex < ViewDocumentPane.Children.Count
                && ViewDocumentPane.Children[selectedIndex] is LayoutDocument selectedDocument
                && selectedDocument.Content is ConoscopeView selectedView)
            {
                return selectedView;
            }

            return null;
        }

        private ConoscopeView[] GetOpenViews()
        {
            return ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .Select(item => item.Content as ConoscopeView)
                .Where(item => item != null)
                .Cast<ConoscopeView>()
                .ToArray();
        }

        private void SaveAllViewLayouts()
        {
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.LayoutManager?.SaveLayout();
            }
        }

        private static string GetContentId(string filePath)
        {
            return "StandaloneConoscope:" + Tool.GetMD5(Path.GetFullPath(filePath));
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                RestoreDirectory = true,
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    OpenConoscope(filename);
                }
            }
        }

        private void btnExportAngleMode_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportAngleMode();
        }

        private void btnExportCircleMode_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportCircleMode();
        }

        private void btnAdvancedExport_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.AdvancedExport();
        }

        private void cbModelType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingModelSelection || cbModelType.SelectedItem is not ConoscopeModelType conoscopeModelType)
            {
                return;
            }

            ConoscopeManager.GetInstance().Config.CurrentModel = conoscopeModelType;
        }

        private void btnOpenObservationCamera_Click(object sender, RoutedEventArgs e)
        {
            if (observationCameraWindow != null && observationCameraWindow.IsVisible)
            {
                observationCameraWindow.Activate();
                return;
            }

            observationCameraWindow = new MVSViewWindow();
            observationCameraWindow.Closed += (s, e) =>
            {
                observationCameraWindow = null;
                tbObservationCameraStatus.Text = Properties.Resources.NotOpened;
                tbObservationCameraStatus.Foreground = Brushes.Gray;
            };
            tbObservationCameraStatus.Text = "已打开";
            tbObservationCameraStatus.Foreground = Brushes.LimeGreen;
            observationCameraWindow.Show();
        }

        public void Dispose()
        {
            ConoscopeManager.GetInstance().Config.ModelTypeChanged -= ConoscopeConfig_ModelTypeChanged;
            if (themeChangedHandler != null)
            {
                ThemeManager.Current.CurrentUIThemeChanged -= themeChangedHandler;
                themeChangedHandler = null;
            }

            foreach (ConoscopeView view in GetOpenViews())
            {
                view.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
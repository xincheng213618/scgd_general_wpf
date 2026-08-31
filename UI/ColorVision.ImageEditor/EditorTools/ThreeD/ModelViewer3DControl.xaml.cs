using ColorVision.UI;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model.Scene;
using HelixToolkit.SharpDX.Utilities;
using HelixToolkit.Wpf.SharpDX;
using log4net;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    public partial class ModelViewer3DControl : UserControl, IDisposable, IActiveDocumentStatusProvider
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ModelViewer3DControl));
        private readonly DefaultEffectsManager? effectsManager;
        private readonly SceneNodeGroupModel3D sceneGroup = new();
        private readonly LatestModelLoadCoordinator<ModelViewer3DModel> loadCoordinator = new();
        private readonly SceneVisibilityState visibilityState = new();
        private readonly HashSet<ModelViewer3DModel> retainedModels = new();
        private readonly DispatcherTimer toastTimer;
        private readonly TaskCompletionSource loadedSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ModelViewer3DSession session = new(new ModelViewerDefaults(ModelViewerRenderMode.Textured, ModelViewerProjection.Perspective));
        private ModelViewer3DModel? currentModel;
        private ModelViewerSceneItem? selectedItem;
        private Task? initialLoadTask;
        private string? initialLoadTaskPath;
        private string? initialFilePath;
        private CancellationTokenSource? exportCancellation;
        private string currentViewName = "ISO";
        private long loadVersion;
        private bool hasGraphicsFault;
        private bool notifyExportCancellation;
        private bool isVerticalFlipped;
        private bool isExporting;
        private bool isUiReady;
        private bool isDisposed;

        public static ModelViewer3DConfig Config => ModelViewer3DConfig.Instance;
        private bool IsGraphicsAvailable => effectsManager != null && !hasGraphicsFault;

        public ModelViewer3DControl()
        {
            InitializeComponent();
            // Participate in the configurable application Save As action while preserving
            // the standalone viewer's existing snapshot shortcut and output semantics.
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs,
                (sender, e) => { Screenshot_Click(sender, e); e.Handled = true; },
                (_, e) => { e.CanExecute = currentModel != null && !isExporting && session.LoadState != ModelViewerLoadState.Loading; e.Handled = true; }));
            Viewport.RenderExceptionOccurred += Viewport_RenderExceptionOccurred;

            ModelViewer3DConfig? config = TryGetConfig();
            ModelViewerRenderMode initialRenderMode = config?.DefaultWireframe == true
                ? ModelViewerRenderMode.Wireframe
                : config?.IsTextureVisible == false ? ModelViewerRenderMode.Solid : ModelViewerRenderMode.Textured;
            session = new ModelViewer3DSession(new ModelViewerDefaults(initialRenderMode, ModelViewerProjection.Perspective));

            DefaultEffectsManager? manager = null;
            try
            {
                manager = new DefaultEffectsManager();
                Viewport.EffectsManager = manager;
            }
            catch (Exception ex)
            {
                Log.Error("Direct3D initialization failed for the 3D model viewer.", ex);
                hasGraphicsFault = true;
                try
                {
                    Viewport.EffectsManager = null;
                }
                catch (Exception resetError)
                {
                    Log.Debug("The failed Direct3D effects manager could not be detached from the viewport.", resetError);
                }
                try
                {
                    manager?.Dispose();
                }
                catch (Exception disposeError)
                {
                    Log.Debug("The failed Direct3D effects manager could not be released cleanly.", disposeError);
                }
                manager = null;
            }
            effectsManager = manager;
            ScenePresenter.Content = sceneGroup;
            PerspectiveCamera.FieldOfView = Math.Clamp(config?.FieldOfView ?? 60, 10, 120);
            ToolbarPanel.Visibility = Visibility.Visible;
            if (config?.IsToolbarVisible == false)
                config.IsToolbarVisible = true;

            toastTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(4),
            };
            toastTimer.Tick += ToastTimer_Tick;

            Loaded += ModelViewer3DControl_Loaded;
            isUiReady = true;
            SelectRenderModeInCombo(initialRenderMode);
            UpdateUiState();
        }

        public event EventHandler? StatusBarItemsChanged;

        private void ModelViewer3DControl_Loaded(object sender, RoutedEventArgs e)
        {
            loadedSignal.TrySetResult();
            Focus();
            Keyboard.Focus(this);

            if (IsGraphicsAvailable && initialLoadTask == null && currentModel == null && !string.IsNullOrWhiteSpace(initialFilePath))
                StartInitialLoad(initialFilePath);
        }

        public void SetInitialFile(string? filePath)
        {
            initialFilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
            if (initialLoadTask?.IsCompleted == true && (!PathsEqual(initialLoadTaskPath, initialFilePath) || currentModel == null))
            {
                initialLoadTask = null;
                initialLoadTaskPath = null;
            }
            if (IsGraphicsAvailable && IsLoaded && initialLoadTask == null && currentModel == null && initialFilePath != null)
                StartInitialLoad(initialFilePath);
        }

        public async Task InitializeAndLoadAsync(string filePath)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            string targetPath = Path.GetFullPath(filePath);
            SetInitialFile(targetPath);

            if (!IsLoaded)
                await loadedSignal.Task.ConfigureAwait(true);

            ObjectDisposedException.ThrowIf(isDisposed, this);
            Task? targetInitialLoad = PathsEqual(initialLoadTaskPath, targetPath) ? initialLoadTask : null;
            if (targetInitialLoad != null)
            {
                await targetInitialLoad.ConfigureAwait(true);
                return;
            }

            if (!PathsEqual(currentModel?.FilePath, targetPath))
                await LoadModelAsync(targetPath).ConfigureAwait(true);
        }

        private void StartInitialLoad(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);
            initialLoadTaskPath = fullPath;
            initialLoadTask = LoadModelAsync(fullPath);
        }

        public async Task LoadModelAsync(string filePath)
        {
            if (isDisposed || !IsGraphicsAvailable)
                return;

            CancelExport(false);
            long requestVersion = Interlocked.Increment(ref loadVersion);
            loadCoordinator.CancelActive();

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch (Exception ex)
            {
                ShowLoadFailure(ex);
                return;
            }

            session.BeginLoad(fullPath);
            LoadingText.Text = FormatResource(Properties.Resources.ThreeD_LoadingFileFormat, Path.GetFileName(fullPath));
            LoadingPanel.Visibility = Visibility.Visible;
            ToastPanel.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = currentModel == null ? Visibility.Collapsed : Visibility.Visible;
            UpdateUiState();

            ModelLoadOperationResult<ModelViewer3DModel> result;
            try
            {
                result = await loadCoordinator.RunAsync(token => ModelViewer3DLoader.LoadAsync(fullPath, token));
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (isDisposed || requestVersion != Interlocked.Read(ref loadVersion) || result.Status == ModelLoadOperationStatus.Superseded)
            {
                result.Value?.Dispose();
                return;
            }

            LoadingPanel.Visibility = Visibility.Collapsed;
            switch (result.Status)
            {
                case ModelLoadOperationStatus.Succeeded when result.Value != null:
                    try
                    {
                        CommitModel(result.Value);
                    }
                    catch (Exception ex)
                    {
                        ShowLoadFailure(ex);
                    }
                    break;
                case ModelLoadOperationStatus.Canceled:
                    session.CancelLoad();
                    ShowToast(Properties.Resources.ThreeD_LoadCanceled, false);
                    UpdateUiState();
                    break;
                case ModelLoadOperationStatus.Failed:
                    ShowLoadFailure(result.Error ?? new ModelViewerLoadException("Unknown model loading failure."));
                    break;
            }
        }

        public void DisposeViewer()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Interlocked.Increment(ref loadVersion);
            loadedSignal.TrySetCanceled();
            Loaded -= ModelViewer3DControl_Loaded;
            toastTimer.Stop();
            toastTimer.Tick -= ToastTimer_Tick;
            loadCoordinator.Dispose();
            CancellationTokenSource? activeExport = exportCancellation;
            exportCancellation = null;
            activeExport?.Cancel();
            isExporting = false;

            Viewport.MouseDown3D -= Viewport_MouseDown3D;
            Viewport.MouseDoubleClick -= Viewport_MouseDoubleClick;
            Viewport.RenderExceptionOccurred -= Viewport_RenderExceptionOccurred;
            ScenePresenter.Content = null;
            try
            {
                sceneGroup.Clear(false);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to detach all 3D scenes while closing the viewer.", ex);
            }
            try
            {
                currentModel?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to release the active 3D model while closing the viewer.", ex);
            }
            foreach (ModelViewer3DModel retainedModel in retainedModels)
            {
                if (ReferenceEquals(retainedModel, currentModel))
                    continue;
                try
                {
                    retainedModel.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to release a retained 3D model while closing the viewer.", ex);
                }
            }
            retainedModels.Clear();
            currentModel = null;
            ModelTreeView.ItemsSource = null;
            try
            {
                Viewport.EffectsManager = null;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to detach the 3D effects manager while closing the viewer.", ex);
            }
            try
            {
                Viewport.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to release the 3D viewport while closing the viewer.", ex);
            }
            try
            {
                sceneGroup.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to release the 3D scene group while closing the viewer.", ex);
            }
            try
            {
                effectsManager?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to release the 3D effects manager while closing the viewer.", ex);
            }
        }

        public void Dispose()
        {
            DisposeViewer();
            GC.SuppressFinalize(this);
        }

        public IEnumerable<StatusBarMeta> GetActiveStatusBarItems()
        {
            if (currentModel == null)
                return Array.Empty<StatusBarMeta>();

            ModelViewer3DStatistics statistics = currentModel.Statistics;
            return new[]
            {
                CreateStatusItem("Model3D.File", Properties.Resources.ThreeD_StatusModel, Path.GetFileName(currentModel.FilePath), 100),
                CreateStatusItem("Model3D.Geometry", Properties.Resources.ThreeD_Geometry, FormatResource(Properties.Resources.ThreeD_VerticesTrianglesFormat, FormatCount(statistics.VertexCount), FormatCount(statistics.TriangleCount)), 101),
                CreateStatusItem("Model3D.View", Properties.Resources.ThreeD_View, $"{ProjectionText.Text} · {currentViewName}", 102),
            };
        }

        private StatusBarMeta CreateStatusItem(string id, string name, string description, int order)
        {
            return new StatusBarMeta
            {
                Id = id,
                Name = name,
                Description = description,
                Type = StatusBarType.Text,
                Alignment = StatusBarAlignment.Right,
                Order = order,
                Source = this,
            };
        }

        private void CommitModel(ModelViewer3DModel model)
        {
            ModelViewer3DModel? oldModel = currentModel;
            ModelViewerSceneItem? oldSelection = selectedItem;
            SceneVisibilityState oldVisibility = visibilityState.Clone();
            bool oldVerticalFlipped = isVerticalFlipped;
            string oldSearchText = SceneSearchTextBox.Text;
            ModelViewer3DConfig? config = TryGetConfig();
            bool newVerticalFlipped = config?.AutoOrientModel != false && model.Statistics.SuggestedVerticalFlip;

            bool attached = false;
            try
            {
                SceneVisibilityState initialVisibility = new();
                model.ApplyRenderMode(session.RenderMode);
                model.ApplyVisibility(initialVisibility);
                if (!sceneGroup.AddNode(model.Root))
                    throw new InvalidOperationException("The imported scene could not be attached to the viewport.");
                attached = true;

                currentModel = model;
                visibilityState.Reset();
                selectedItem = null;
                isVerticalFlipped = newVerticalFlipped;
                FlipVerticalMenuItem.IsChecked = isVerticalFlipped;
                Viewport.ModelUpDirection = new Vector3D(0, 0, isVerticalFlipped ? -1 : 1);
                ModelTreeView.ItemsSource = model.SceneItems;
                SceneSearchTextBox.Clear();
                UpdateModelDetails(model);
                UpdateGroundGrid(model.Statistics);
                UpdateSelection(null);
                UpdateUiState();
            }
            catch (Exception commitError)
            {
                attached |= IsSceneAttached(model.Root);
                if (!RollbackModelCommit(model, attached, oldModel, oldSelection, oldVisibility, oldVerticalFlipped, oldSearchText, commitError))
                {
                    if (oldModel != null)
                        retainedModels.Add(oldModel);
                    retainedModels.Add(model);
                    EnterGraphicsFault(commitError);
                    Log.Fatal("The 3D viewer could not establish a trustworthy scene after rollback and entered graphics fault state.", commitError);
                }
                else
                {
                    model.Dispose();
                }
                throw;
            }

            if (oldModel != null)
            {
                if (TryDetachInactiveModel(oldModel, model, out bool previousRestored))
                {
                    try
                    {
                        oldModel.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Failed to release the previous 3D scene after replacement.", ex);
                    }
                }
                else if (previousRestored)
                {
                    RestorePreviousModelState(oldModel, oldSelection, oldVisibility, oldVerticalFlipped, oldSearchText);
                    model.Dispose();
                    throw new InvalidOperationException("The replacement scene could not be made active; the previous model was restored.");
                }
                else
                {
                    retainedModels.Add(oldModel);
                    retainedModels.Add(model);
                    EnterGraphicsFault(new InvalidOperationException("The 3D scene group could not attach either model after replacement."));
                    throw new InvalidOperationException("The 3D scene replacement failed and the graphics host is no longer trustworthy.");
                }
            }

            session.CompleteLoad(model.FilePath);
            RaiseStatusBarItemsChanged();

            if (config != null)
            {
                try
                {
                    config.LastOpenDirectory = Path.GetDirectoryName(model.FilePath) ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Log.Warn("The 3D viewer could not persist the last model directory.", ex);
                }
            }

            if (!isDisposed)
            {
                try
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                    {
                        if (!isDisposed && ReferenceEquals(currentModel, model))
                        {
                            ApplyViewPreset("Iso");
                            FitView();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log.Debug("The 3D viewer dispatcher stopped before the initial camera frame was queued.", ex);
                }
            }

            try
            {
                string message = model.Statistics.MissingTexturePaths.Count > 0
                    ? FormatResource(Properties.Resources.ThreeD_ModelLoadedMissingTexturesFormat, model.Statistics.MissingTexturePaths.Count)
                    : FormatResource(Properties.Resources.ThreeD_ModelLoadedTrianglesFormat, FormatCount(model.Statistics.TriangleCount));
                if (isVerticalFlipped)
                    message += $" {Properties.Resources.ThreeD_AutoOrientationApplied}";
                ShowToast(message, model.Statistics.MissingTexturePaths.Count > 0);
            }
            catch (Exception ex)
            {
                Log.Warn("The 3D model loaded, but its completion notification could not be shown.", ex);
            }
        }

        private bool RollbackModelCommit(
            ModelViewer3DModel model,
            bool attached,
            ModelViewer3DModel? oldModel,
            ModelViewerSceneItem? oldSelection,
            SceneVisibilityState oldVisibility,
            bool oldVerticalFlipped,
            string oldSearchText,
            Exception commitError)
        {
            bool detached = !attached;
            bool oldSceneAttached = oldModel == null || IsSceneAttached(oldModel.Root);
            if (attached)
            {
                try
                {
                    detached = sceneGroup.RemoveNode(model.Root, false);
                }
                catch (Exception detachError)
                {
                    Log.Error("Failed to detach a 3D scene while rolling back a model replacement.", detachError);
                }

                if (!detached)
                {
                    try
                    {
                        sceneGroup.Clear(false);
                        detached = true;
                        oldSceneAttached = oldModel == null || sceneGroup.AddNode(oldModel.Root);
                        if (!oldSceneAttached)
                            Log.Error("The previous 3D scene could not be reattached after a full rollback detach.");
                    }
                    catch (Exception clearError)
                    {
                        Log.Error("Failed to clear the 3D scene group during rollback.", clearError);
                    }
                }
            }
            if (oldModel != null && detached && !oldSceneAttached)
            {
                try
                {
                    oldSceneAttached = sceneGroup.AddNode(oldModel.Root);
                }
                catch (Exception attachError)
                {
                    Log.Error("Failed to reattach the previous 3D scene during rollback.", attachError);
                }
            }

            if (!detached || !oldSceneAttached)
            {
                if (oldModel != null)
                    retainedModels.Add(oldModel);
                return false;
            }

            RestorePreviousModelState(oldModel, oldSelection, oldVisibility, oldVerticalFlipped, oldSearchText);
            Log.Error("The new 3D scene was rolled back before ownership was transferred.", commitError);
            return true;
        }

        private void RestorePreviousModelState(
            ModelViewer3DModel? oldModel,
            ModelViewerSceneItem? oldSelection,
            SceneVisibilityState oldVisibility,
            bool oldVerticalFlipped,
            string oldSearchText)
        {
            currentModel = oldModel;
            try
            {
                visibilityState.CopyFrom(oldVisibility);
                isVerticalFlipped = oldVerticalFlipped;
                FlipVerticalMenuItem.IsChecked = oldVerticalFlipped;
                Viewport.ModelUpDirection = new Vector3D(0, 0, oldVerticalFlipped ? -1 : 1);
                ModelTreeView.ItemsSource = oldModel?.SceneItems;
                SceneSearchTextBox.Text = oldSearchText;
                if (oldModel != null)
                {
                    oldModel.ApplyVisibility(visibilityState);
                    UpdateModelDetails(oldModel);
                    UpdateGroundGrid(oldModel.Statistics);
                }
                else
                {
                    ClearModelDetails();
                    GroundGrid.Visibility = Visibility.Collapsed;
                    Viewport.FixedRotationPointEnabled = false;
                }
                UpdateSelection(oldSelection);
                UpdateUiState();
            }
            catch (Exception restoreError)
            {
                Log.Error("Failed to restore the previous 3D viewer state after a replacement error.", restoreError);
            }
        }

        private bool TryDetachInactiveModel(ModelViewer3DModel inactiveModel, ModelViewer3DModel activeModel, out bool previousRestored)
        {
            previousRestored = false;
            try
            {
                if (sceneGroup.RemoveNode(inactiveModel.Root, false))
                    return true;
            }
            catch (Exception removeError)
            {
                Log.Error("Failed to detach the previous 3D scene directly; attempting a full scene reset.", removeError);
            }

            try
            {
                sceneGroup.Clear(false);
                if (sceneGroup.AddNode(activeModel.Root))
                    return true;

                Log.Error("The active 3D scene could not be reattached after a full scene reset; restoring the previous scene.");
                previousRestored = sceneGroup.AddNode(inactiveModel.Root);
            }
            catch (Exception resetError)
            {
                Log.Error("Failed to reset the 3D scene group while replacing a model.", resetError);
                try
                {
                    if (!IsSceneAttached(activeModel.Root))
                        previousRestored = IsSceneAttached(inactiveModel.Root) || sceneGroup.AddNode(inactiveModel.Root);
                }
                catch (Exception restoreError)
                {
                    Log.Error("Failed to restore the previous scene after the active scene reset failed.", restoreError);
                }
            }
            return false;
        }

        private bool IsSceneAttached(SceneNode node)
        {
            try
            {
                return sceneGroup.SceneNode.Items.Contains(node);
            }
            catch (Exception ex)
            {
                Log.Warn("The 3D viewer could not inspect scene ownership during rollback.", ex);
                return true;
            }
        }

        private void ClearModelDetails()
        {
            SummaryFileNameText.Text = string.Empty;
            SummaryGeometryText.Text = "—";
            InfoFileNameText.Text = "—";
            InfoMeshCountText.Text = "—";
            InfoMaterialCountText.Text = "—";
            InfoDimensionsText.Text = "—";
            InfoLoadText.Text = "—";
            SelectionNameText.Text = string.Empty;
            SelectionStatsText.Text = "—";
        }

        private void UpdateModelDetails(ModelViewer3DModel model)
        {
            ModelViewer3DStatistics stats = model.Statistics;
            string fileName = Path.GetFileName(model.FilePath);
            SummaryFileNameText.Text = fileName;
            SummaryGeometryText.Text = FormatResource(Properties.Resources.ThreeD_VerticesTrianglesFormat, FormatCount(stats.VertexCount), FormatCount(stats.TriangleCount));
            InfoFileNameText.Text = $"{fileName}\n{FormatFileSize(stats.FileSize)}";
            InfoMeshCountText.Text = stats.MeshCount.ToString("N0", CultureInfo.CurrentCulture);
            InfoMaterialCountText.Text = stats.MaterialCount.ToString("N0", CultureInfo.CurrentCulture);
            InfoDimensionsText.Text = stats.HasBounds
                ? $"X {stats.Width:N2}  ×  Y {stats.Depth:N2}  ×  Z {stats.Height:N2}"
                : Properties.Resources.ThreeD_NoValidBounds;
            InfoLoadText.Text = FormatResource(Properties.Resources.ThreeD_LoadDurationTexturesFormat, stats.LoadDuration.TotalSeconds.ToString("N2", CultureInfo.CurrentCulture), stats.TextureCount.ToString("N0", CultureInfo.CurrentCulture));
        }

        private void UpdateGroundGrid(ModelViewer3DStatistics stats)
        {
            if (!stats.HasBounds)
            {
                GroundGrid.Visibility = Visibility.Collapsed;
                Viewport.FixedRotationPointEnabled = false;
                return;
            }

            float horizontalSpan = Math.Max(stats.Width, stats.Depth);
            float gridOffset = Math.Max(horizontalSpan, stats.Height) * 0.001f;
            GroundGrid.GridSpacing = CalculateNiceGridSpacing(horizontalSpan);
            GroundGrid.Offset = isVerticalFlipped ? stats.Bounds.Maximum.Z + gridOffset : stats.Bounds.Minimum.Z - gridOffset;
            GroundGrid.Visibility = Visibility.Visible;
            SetFixedRotationPoint(stats.Bounds);
        }

        private static double CalculateNiceGridSpacing(float span)
        {
            if (!float.IsFinite(span) || span <= float.Epsilon)
                return 10;

            double rawSpacing = span / 12d;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawSpacing)));
            double normalized = rawSpacing / magnitude;
            double nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
            return nice * magnitude;
        }

        private void SetFixedRotationPoint(BoundingBox bounds)
        {
            Point3D center = new(
                (bounds.Minimum.X + bounds.Maximum.X) * 0.5,
                (bounds.Minimum.Y + bounds.Maximum.Y) * 0.5,
                (bounds.Minimum.Z + bounds.Maximum.Z) * 0.5);
            Viewport.FixedRotationPoint = center;
            Viewport.FixedRotationPointEnabled = true;
        }

        private void UpdateUiState()
        {
            bool hasModel = currentModel != null;
            bool hasSelection = selectedItem != null;
            bool isLoading = session.LoadState == ModelViewerLoadState.Loading;
            bool isFileOperationBusy = isLoading || isExporting;
            bool graphicsAvailable = IsGraphicsAvailable;

            GraphicsErrorPanel.Visibility = graphicsAvailable ? Visibility.Collapsed : Visibility.Visible;
            EmptyStatePanel.Visibility = graphicsAvailable && !hasModel && !isLoading ? Visibility.Visible : Visibility.Collapsed;
            ModelSummaryCard.Visibility = hasModel ? Visibility.Visible : Visibility.Collapsed;
            OpenModelButton.IsEnabled = graphicsAvailable && !isExporting;
            FitViewButton.IsEnabled = hasModel;
            FocusSelectionButton.IsEnabled = hasSelection;
            ViewPresetComboBox.IsEnabled = hasModel;
            ProjectionToggle.IsEnabled = hasModel;
            RenderModeComboBox.IsEnabled = hasModel;
            ScreenshotButton.IsEnabled = hasModel && !isFileOperationBusy;
            MoreButton.IsEnabled = hasModel && !isFileOperationBusy;
            SelectionCard.Visibility = hasSelection && ActualWidth >= 820 ? Visibility.Visible : Visibility.Collapsed;
            IsolationBanner.Visibility = visibilityState.IsIsolated ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateSelection(ModelViewerSceneItem? item)
        {
            selectedItem = item;
            session.SelectedNodeId = item?.Id;
            currentModel?.SetSelected(item);

            if (item != null)
            {
                long triangles = item.SelfAndDescendants().Sum(sceneItem => sceneItem.TriangleCount);
                string material = string.IsNullOrWhiteSpace(item.MaterialName) ? (item.IsMesh ? Properties.Resources.ThreeD_DefaultMaterial : Properties.Resources.ThreeD_ObjectGroup) : item.MaterialName;
                SelectionNameText.Text = item.Name;
                SelectionStatsText.Text = FormatResource(Properties.Resources.ThreeD_SelectionTrianglesMaterialFormat, FormatCount(triangles), material);
            }

            UpdateUiState();
        }

        private void ApplyVisibility()
        {
            currentModel?.ApplyVisibility(visibilityState);
            IsolationBanner.Visibility = visibilityState.IsIsolated ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SelectItem(ModelViewerSceneItem? item)
        {
            if (ReferenceEquals(selectedItem, item))
                return;
            UpdateSelection(item);
        }

        private void Viewport_MouseDown3D(object sender, RoutedEventArgs e)
        {
            if (e is MouseDown3DEventArgs { HitTestResult.ModelHit: SceneNode node })
                SelectItem(currentModel?.FindItem(node));
        }

        private void Viewport_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && selectedItem != null)
            {
                FocusSelection();
                e.Handled = true;
            }
        }

        private void Viewport_RenderExceptionOccurred(object? sender, RelayExceptionEventArgs e)
        {
            Log.Error("The 3D viewport render host failed.", e.Exception);
            if (isDisposed)
                return;

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Send, () => EnterGraphicsFault(e.Exception));
                }
                catch (Exception dispatchError)
                {
                    Log.Error("The 3D graphics failure could not be reported on the UI thread.", dispatchError);
                }
                return;
            }

            EnterGraphicsFault(e.Exception);
        }

        private void EnterGraphicsFault(Exception exception)
        {
            if (isDisposed || hasGraphicsFault)
                return;

            hasGraphicsFault = true;
            Interlocked.Increment(ref loadVersion);
            loadCoordinator.CancelActive();
            CancelExport(false);
            session.CancelLoad();
            LoadingPanel.Visibility = Visibility.Collapsed;
            ToastPanel.Visibility = Visibility.Collapsed;
            Log.Error("The 3D model viewer entered a persistent graphics fault state.", exception);
            UpdateUiState();
        }

        private void ModelTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ModelViewerSceneItem item)
                SelectItem(item);
        }

        private void NodeVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (currentModel == null || sender is not FrameworkElement { DataContext: ModelViewerSceneItem item })
                return;

            if (visibilityState.IsIsolated)
                visibilityState.ExitIsolation();
            bool hide = !visibilityState.IsHidden(item.Id);
            visibilityState.SetHidden(item.SelfAndDescendants().Select(sceneItem => sceneItem.Id), hide);
            ApplyVisibility();
            ShowToast(FormatResource(hide ? Properties.Resources.ThreeD_HiddenFormat : Properties.Resources.ThreeD_ShownFormat, item.Name), false);
            e.Handled = true;
        }

        private void OpenModel_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = Properties.Resources.ThreeD_OpenModel,
                Filter = Properties.Resources.ThreeD_ModelFileFilter,
                InitialDirectory = GetInitialDirectory(),
                CheckFileExists = true,
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) == true)
                _ = LoadModelAsync(dialog.FileName);
        }

        private void FitView_Click(object sender, RoutedEventArgs e) => FitView();

        private void FitView()
        {
            if (currentModel == null)
                return;
            if (currentModel.Statistics.HasBounds)
            {
                FrameBounds(currentModel.Statistics.Bounds, Viewport.Camera?.LookDirection, Viewport.Camera?.UpDirection);
            }
            else
            {
                Viewport.ZoomExtents(350);
            }
        }

        private void FocusSelection_Click(object sender, RoutedEventArgs e) => FocusSelection();

        private void FocusSelection()
        {
            if (selectedItem == null || !selectedItem.Node.HasBound)
                return;

            BoundingBox bounds = selectedItem.Node.BoundsWithTransform;
            FrameBounds(bounds, Viewport.Camera?.LookDirection, Viewport.Camera?.UpDirection);
        }

        private void IsolateSelected_Click(object sender, RoutedEventArgs e)
        {
            if (currentModel == null || selectedItem == null)
                return;

            visibilityState.EnterIsolation(ModelViewer3DModel.GetIsolationScope(selectedItem));
            ApplyVisibility();
            ShowToast(FormatResource(Properties.Resources.ThreeD_IsolatingFormat, selectedItem.Name), false);
        }

        private void ExitIsolation_Click(object sender, RoutedEventArgs e)
        {
            visibilityState.ExitIsolation();
            ApplyVisibility();
            ShowToast(Properties.Resources.ThreeD_IsolationExited, false);
        }

        private void HideSelected_Click(object sender, RoutedEventArgs e)
        {
            if (selectedItem == null)
                return;

            string name = selectedItem.Name;
            if (visibilityState.IsIsolated)
                visibilityState.ExitIsolation();
            visibilityState.SetHidden(selectedItem.SelfAndDescendants().Select(sceneItem => sceneItem.Id), true);
            ApplyVisibility();
            UpdateSelection(null);
            ShowToast(FormatResource(Properties.Resources.ThreeD_HiddenFormat, name), false);
        }

        private void ShowAllNodes_Click(object sender, RoutedEventArgs e)
        {
            visibilityState.ShowAll();
            ApplyVisibility();
            ShowToast(Properties.Resources.ThreeD_ShownAll, false);
        }

        private void ViewPresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isUiReady || ViewPresetComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string preset)
                return;
            ApplyViewPreset(preset);
        }

        private void ApplyViewPreset(string preset)
        {
            if (currentModel == null)
                return;

            double verticalSign = isVerticalFlipped ? -1 : 1;
            (Vector3D lookDirection, Vector3D upDirection, string name) = preset switch
            {
                "Front" => (new Vector3D(0, 1, 0), new Vector3D(0, 0, verticalSign), Properties.Resources.ThreeD_Front),
                "Back" => (new Vector3D(0, -1, 0), new Vector3D(0, 0, verticalSign), Properties.Resources.ThreeD_Back),
                "Left" => (new Vector3D(1, 0, 0), new Vector3D(0, 0, verticalSign), Properties.Resources.ThreeD_Left),
                "Right" => (new Vector3D(-1, 0, 0), new Vector3D(0, 0, verticalSign), Properties.Resources.ThreeD_Right),
                "Top" => (new Vector3D(0, 0, -verticalSign), new Vector3D(0, 1, 0), Properties.Resources.ThreeD_Top),
                "Bottom" => (new Vector3D(0, 0, verticalSign), new Vector3D(0, -1, 0), Properties.Resources.ThreeD_Bottom),
                _ => (new Vector3D(-1, 1, -verticalSign), new Vector3D(0, 0, verticalSign), Properties.Resources.ThreeD_IsoViewToolTip),
            };
            currentViewName = name;
            if (currentModel.Statistics.HasBounds)
                FrameBounds(currentModel.Statistics.Bounds, lookDirection, upDirection);
            RaiseStatusBarItemsChanged();
        }

        private void ProjectionToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!isUiReady)
                return;

            session.Projection = ProjectionToggle.IsChecked == true ? ModelViewerProjection.Orthographic : ModelViewerProjection.Perspective;
            Viewport.Orthographic = session.Projection == ModelViewerProjection.Orthographic;
            GroundGrid.AutoSpacingRate = session.Projection == ModelViewerProjection.Orthographic ? 20 : 5;
            ProjectionText.Text = session.Projection == ModelViewerProjection.Orthographic ? Properties.Resources.ThreeD_Orthographic : Properties.Resources.ThreeD_Perspective;
            FitView();
            RaiseStatusBarItemsChanged();
        }

        private void RenderModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isUiReady || RenderModeComboBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag || !Enum.TryParse(tag, out ModelViewerRenderMode mode))
                return;

            session.RenderMode = mode;
            currentModel?.ApplyRenderMode(mode);
            PersistRenderModeDefaults(mode);
        }

        private void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            ModelViewer3DModel? model = currentModel;
            long operationVersion = Interlocked.Read(ref loadVersion);
            if (model == null || isExporting || session.LoadState == ModelViewerLoadState.Loading)
                return;

            SaveFileDialog dialog = new()
            {
                Title = Properties.Resources.ThreeD_Screenshot,
                Filter = Properties.Resources.ThreeD_PngFileFilter,
                FileName = $"{Path.GetFileNameWithoutExtension(model.FilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                InitialDirectory = GetInitialDirectory(),
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;
            if (!IsCurrentModel(model, operationVersion))
            {
                ShowToast(Properties.Resources.ThreeD_ModelChangedRetry, true);
                return;
            }

            try
            {
                int width = Math.Max(1, (int)Viewport.ActualWidth);
                int height = Math.Max(1, (int)Viewport.ActualHeight);
                BitmapSource? bitmap = ViewportExtensions.RenderBitmap(Viewport, width, height);
                if (bitmap == null)
                    throw new InvalidOperationException("The viewport did not return an image.");

                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream stream = new(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
                encoder.Save(stream);
                ShowToast(FormatResource(Properties.Resources.ThreeD_ScreenshotSavedFormat, Path.GetFileName(dialog.FileName)), false);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to capture the 3D viewport.", ex);
                ShowToast(Properties.Resources.ThreeD_ScreenshotFailed, true);
            }
        }

        private async void ExportModel_Click(object sender, RoutedEventArgs e)
        {
            ModelViewer3DModel? model = currentModel;
            long operationVersion = Interlocked.Read(ref loadVersion);
            if (model == null || isExporting || session.LoadState == ModelViewerLoadState.Loading)
                return;

            SaveFileDialog dialog = new()
            {
                Title = Properties.Resources.ThreeD_ExportModel,
                Filter = Properties.Resources.ThreeD_ExportFileFilter,
                FileName = Path.GetFileNameWithoutExtension(model.FilePath),
                InitialDirectory = GetInitialDirectory(),
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;
            if (!IsCurrentModel(model, operationVersion))
            {
                ShowToast(Properties.Resources.ThreeD_ModelChangedRetry, true);
                return;
            }

            CancellationTokenSource cancellation = new();
            exportCancellation = cancellation;
            notifyExportCancellation = false;
            isExporting = true;
            LoadingText.Text = FormatResource(Properties.Resources.ThreeD_ExportingFileFormat, Path.GetFileName(dialog.FileName));
            LoadingPanel.Visibility = Visibility.Visible;
            UpdateUiState();
            try
            {
                string formatId = Path.GetExtension(dialog.FileName).TrimStart('.').ToLowerInvariant();
                ErrorCode result = await ModelViewer3DLoader.ExportAsync(model.FilePath, dialog.FileName, formatId, cancellation.Token).ConfigureAwait(true);
                if (!result.HasFlag(ErrorCode.Succeed))
                    throw new InvalidOperationException($"Assimp export failed ({result}).");
                if (!isDisposed)
                    ShowToast(FormatResource(Properties.Resources.ThreeD_ExportSucceededFormat, Path.GetFileName(dialog.FileName)), false);
            }
            catch (OperationCanceledException)
            {
                if (!isDisposed && notifyExportCancellation)
                    ShowToast(Properties.Resources.ThreeD_ExportCanceled, false);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to export the 3D model.", ex);
                if (!isDisposed)
                    ShowToast(Properties.Resources.ThreeD_ExportFailed, true);
            }
            finally
            {
                if (ReferenceEquals(exportCancellation, cancellation))
                {
                    exportCancellation = null;
                    isExporting = false;
                    notifyExportCancellation = false;
                    if (!isDisposed)
                    {
                        if (session.LoadState != ModelViewerLoadState.Loading)
                            LoadingPanel.Visibility = Visibility.Collapsed;
                        UpdateUiState();
                    }
                }
                cancellation.Dispose();
            }
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            if (currentModel != null)
                _ = LoadModelAsync(currentModel.FilePath);
        }

        private void FlipVertical_Click(object sender, RoutedEventArgs e)
        {
            if (currentModel == null)
                return;

            isVerticalFlipped = FlipVerticalMenuItem.IsChecked;
            Viewport.ModelUpDirection = new Vector3D(0, 0, isVerticalFlipped ? -1 : 1);
            UpdateGroundGrid(currentModel.Statistics);
            string preset = (ViewPresetComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Iso";
            ApplyViewPreset(preset);
            ShowToast(isVerticalFlipped ? Properties.Resources.ThreeD_VerticalFlipped : Properties.Resources.ThreeD_VerticalRestored, false);
        }

        private void CancelLoad_Click(object sender, RoutedEventArgs e)
        {
            if (session.LoadState == ModelViewerLoadState.Loading)
            {
                Interlocked.Increment(ref loadVersion);
                loadCoordinator.CancelActive();
                session.CancelLoad();
                LoadingPanel.Visibility = isExporting ? Visibility.Visible : Visibility.Collapsed;
                ShowToast(Properties.Resources.ThreeD_LoadCanceled, false);
                UpdateUiState();
                return;
            }

            CancelExport(true);
        }

        private void CancelExport(bool notify)
        {
            CancellationTokenSource? cancellation = exportCancellation;
            if (cancellation == null)
                return;

            notifyExportCancellation |= notify;
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The async export completed between reading and canceling the source.
            }
        }

        private bool IsCurrentModel(ModelViewer3DModel model, long operationVersion)
        {
            return !isDisposed
                && ReferenceEquals(currentModel, model)
                && operationVersion == Interlocked.Read(ref loadVersion)
                && session.LoadState != ModelViewerLoadState.Loading;
        }

        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (MoreButton.ContextMenu == null)
                return;
            MoreButton.ContextMenu.PlacementTarget = MoreButton;
            MoreButton.ContextMenu.IsOpen = true;
        }

        private void SceneRailButton_Click(object sender, RoutedEventArgs e)
        {
            if (SceneRailButton.IsChecked == true)
                ShowDrawer(scene: true);
            else
                SideDrawer.Visibility = Visibility.Collapsed;
        }

        private void InfoRailButton_Click(object sender, RoutedEventArgs e)
        {
            if (InfoRailButton.IsChecked == true)
                ShowDrawer(scene: false);
            else
                SideDrawer.Visibility = Visibility.Collapsed;
        }

        private void ShowDrawer(bool scene)
        {
            SideDrawer.Visibility = Visibility.Visible;
            SceneRailButton.IsChecked = scene;
            InfoRailButton.IsChecked = !scene;
            SceneDrawerContent.Visibility = scene ? Visibility.Visible : Visibility.Collapsed;
            InfoDrawerContent.Visibility = scene ? Visibility.Collapsed : Visibility.Visible;
            DrawerTitle.Text = scene ? Properties.Resources.ThreeD_Scene : Properties.Resources.ThreeD_ModelInformation;
            FitView();
        }

        private void CloseDrawer_Click(object sender, RoutedEventArgs e)
        {
            SideDrawer.Visibility = Visibility.Collapsed;
            SceneRailButton.IsChecked = false;
            InfoRailButton.IsChecked = false;
            FitView();
        }

        private void SceneSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isUiReady)
                currentModel?.ApplyFilter(SceneSearchTextBox.Text);
        }

        private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
            {
                OpenModel_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.R)
            {
                Reload_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.S)
            {
                Screenshot_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.FocusedElement is TextBoxBase or ComboBox)
            {
                return;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Home)
            {
                FitView();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F)
            {
                FocusSelection();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.I)
            {
                IsolateSelected_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.H)
            {
                ShowAllNodes_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.H)
            {
                HideSelected_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && (e.Key is Key.P or Key.NumPad5))
            {
                ProjectionToggle.IsChecked = ProjectionToggle.IsChecked != true;
                ProjectionToggle_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
            {
                if (visibilityState.IsIsolated)
                    ExitIsolation_Click(this, new RoutedEventArgs());
                else
                    UpdateSelection(null);
                e.Handled = true;
            }
        }

        private void Root_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
        }

        private void Root_DragEnter(object sender, DragEventArgs e)
        {
            if (TryGetDroppedModel(e.Data, out _))
            {
                e.Effects = DragDropEffects.Copy;
                DropOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Root_DragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        private void Root_Drop(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
            if (TryGetDroppedModel(e.Data, out string? filePath) && filePath != null)
                _ = LoadModelAsync(filePath);
            e.Handled = true;
        }

        private static bool TryGetDroppedModel(IDataObject data, out string? filePath)
        {
            filePath = null;
            if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] paths)
                return false;

            filePath = paths.FirstOrDefault(path => File.Exists(path) && IsSupportedModel(path));
            return filePath != null;
        }

        private void Root_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ToolbarPanel.MaxWidth = Math.Max(320, ActualWidth - 112);
            bool compactToolbar = ActualWidth < 1060;
            OpenModelText.Visibility = compactToolbar ? Visibility.Collapsed : Visibility.Visible;
            FitViewText.Visibility = compactToolbar ? Visibility.Collapsed : Visibility.Visible;
            FocusSelectionText.Visibility = compactToolbar ? Visibility.Collapsed : Visibility.Visible;
            DisplayModeText.Visibility = ActualWidth < 940 ? Visibility.Collapsed : Visibility.Visible;
            ScreenshotButton.Visibility = ActualWidth < 1120 ? Visibility.Collapsed : Visibility.Visible;
            if (ActualWidth < 620)
                SideDrawer.Visibility = Visibility.Collapsed;
            UpdateUiState();
        }

        private void ShowLoadFailure(Exception exception)
        {
            string message = exception switch
            {
                FileNotFoundException => Properties.Resources.ThreeD_LoadFailedFileMissing,
                NotSupportedException => Properties.Resources.ThreeD_LoadFailedUnsupported,
                UnauthorizedAccessException => Properties.Resources.ThreeD_LoadFailedNoPermission,
                ModelViewerLoadException => Properties.Resources.ThreeD_LoadFailedParse,
                IOException => Properties.Resources.ThreeD_LoadFailedFileBusy,
                _ => Properties.Resources.ThreeD_LoadFailed,
            };
            Log.Error("Failed to load a 3D model.", exception);
            session.FailLoad(message);
            LoadingPanel.Visibility = Visibility.Collapsed;
            ShowToast(message, true);
            UpdateUiState();
        }

        private void ShowToast(string message, bool warning)
        {
            ToastText.Text = message;
            ToastGlyph.Text = warning ? "\uE7BA" : "\uE73E";
            ToastGlyph.Foreground = new SolidColorBrush(warning ? System.Windows.Media.Color.FromRgb(238, 169, 71) : System.Windows.Media.Color.FromRgb(69, 199, 123));
            ToastPanel.Visibility = Visibility.Visible;
            toastTimer.Stop();
            toastTimer.Interval = TimeSpan.FromSeconds(warning ? 7 : 4);
            toastTimer.Start();
        }

        private void ToastTimer_Tick(object? sender, EventArgs e)
        {
            toastTimer.Stop();
            ToastPanel.Visibility = Visibility.Collapsed;
        }

        private void RaiseStatusBarItemsChanged()
        {
            try
            {
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Warn("A status bar subscriber failed while the 3D viewer state changed.", ex);
            }
        }

        private void SelectRenderModeInCombo(ModelViewerRenderMode mode)
        {
            foreach (ComboBoxItem item in RenderModeComboBox.Items)
            {
                if (string.Equals(item.Tag as string, mode.ToString(), StringComparison.Ordinal))
                {
                    RenderModeComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private static void PersistRenderModeDefaults(ModelViewerRenderMode mode)
        {
            ModelViewer3DConfig? config = TryGetConfig();
            if (config == null)
                return;
            config.DefaultWireframe = mode == ModelViewerRenderMode.Wireframe;
            config.IsTextureVisible = mode == ModelViewerRenderMode.Textured;
            config.IsMaterialVisible = mode != ModelViewerRenderMode.Wireframe;
        }

        private static ModelViewer3DConfig? TryGetConfig()
        {
            try
            {
                return Config;
            }
            catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException)
            {
                return null;
            }
        }

        private static bool IsSupportedModel(string path)
        {
            string extension = Path.GetExtension(path);
            return extension.Equals(".obj", StringComparison.OrdinalIgnoreCase) || extension.Equals(".stl", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string? left, string? right)
        {
            return left != null && right != null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetInitialDirectory()
        {
            string? directory = TryGetConfig()?.LastOpenDirectory;
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                ? directory
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private static string FormatCount(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

        private static string FormatResource(string format, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

        private static string FormatFileSize(long bytes)
        {
            const double megabyte = 1024d * 1024d;
            return bytes >= megabyte ? $"{bytes / megabyte:N1} MB" : $"{bytes / 1024d:N1} KB";
        }

        private void FrameBounds(BoundingBox bounds, Vector3D? requestedLookDirection, Vector3D? requestedUpDirection)
        {
            double sizeX = bounds.Maximum.X - bounds.Minimum.X;
            double sizeY = bounds.Maximum.Y - bounds.Minimum.Y;
            double sizeZ = bounds.Maximum.Z - bounds.Minimum.Z;
            double radius = Math.Sqrt(sizeX * sizeX + sizeY * sizeY + sizeZ * sizeZ) * 0.5;
            if (!double.IsFinite(sizeX) || !double.IsFinite(sizeY) || !double.IsFinite(sizeZ) || !double.IsFinite(radius)
                || radius <= double.Epsilon || Viewport.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera camera)
                return;

            Vector3D lookDirection = requestedLookDirection is { LengthSquared: > 0.000001 }
                ? requestedLookDirection.Value
                : new Vector3D(-1, 1, -0.8);
            lookDirection.Normalize();
            Vector3D upDirection = requestedUpDirection is { LengthSquared: > 0.000001 }
                ? requestedUpDirection.Value
                : new Vector3D(0, 0, 1);
            Vector3D rightDirection = Vector3D.CrossProduct(lookDirection, upDirection);
            if (rightDirection.LengthSquared < 0.000001)
            {
                upDirection = Math.Abs(lookDirection.Z) > 0.9 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
                rightDirection = Vector3D.CrossProduct(lookDirection, upDirection);
            }
            rightDirection.Normalize();
            upDirection = Vector3D.CrossProduct(rightDirection, lookDirection);
            upDirection.Normalize();

            Point3D center = new(
                (bounds.Minimum.X + bounds.Maximum.X) * 0.5,
                (bounds.Minimum.Y + bounds.Maximum.Y) * 0.5,
                (bounds.Minimum.Z + bounds.Maximum.Z) * 0.5);
            double viewportWidth = Math.Max(1, Viewport.ActualWidth);
            double viewportHeight = Math.Max(1, Viewport.ActualHeight);
            double reservedLeft = SideDrawer.Visibility == Visibility.Visible ? SideDrawer.Margin.Left + SideDrawer.Width : 0;
            double aspect = Math.Max(0.2, viewportWidth / viewportHeight);
            double contentAspect = Math.Max(0.2, (viewportWidth - reservedLeft) / viewportHeight);
            double fieldOfView = camera is HelixToolkit.Wpf.SharpDX.PerspectiveCamera perspective ? perspective.FieldOfView : PerspectiveCamera.FieldOfView;
            double verticalTangent = Math.Tan(fieldOfView * Math.PI / 360);
            double horizontalTangent = verticalTangent * contentAspect;
            double distance = 0;
            double projectedHalfWidth = 0;
            double projectedHalfHeight = 0;
            foreach (Point3D corner in GetBoundingCorners(bounds))
            {
                Vector3D offset = corner - center;
                double depth = Vector3D.DotProduct(offset, lookDirection);
                double horizontal = Math.Abs(Vector3D.DotProduct(offset, rightDirection));
                double vertical = Math.Abs(Vector3D.DotProduct(offset, upDirection));
                distance = Math.Max(distance, Math.Max(horizontal / horizontalTangent - depth, vertical / verticalTangent - depth));
                projectedHalfWidth = Math.Max(projectedHalfWidth, horizontal);
                projectedHalfHeight = Math.Max(projectedHalfHeight, vertical);
            }
            distance = Math.Max(radius, distance) * 1.12;
            if (!double.IsFinite(distance))
                return;

            double lateralOffset = reservedLeft / viewportHeight * distance * verticalTangent;
            Point3D visualTarget = center - rightDirection * lateralOffset;

            camera.Position = visualTarget - lookDirection * distance;
            camera.LookDirection = lookDirection * distance;
            camera.UpDirection = upDirection;
            camera.NearPlaneDistance = Math.Max(radius * 0.0001, distance - radius * 1.05);
            camera.FarPlaneDistance = Math.Max(camera.NearPlaneDistance * 10, distance + radius * 1.5);
            if (camera is HelixToolkit.Wpf.SharpDX.OrthographicCamera orthographic)
                orthographic.Width = Math.Max(projectedHalfWidth * 2 * aspect / contentAspect, projectedHalfHeight * 2 * aspect) * 1.12;

            Viewport.FixedRotationPoint = center;
            Viewport.FixedRotationPointEnabled = true;
        }

        private static IEnumerable<Point3D> GetBoundingCorners(BoundingBox bounds)
        {
            float[] x = { bounds.Minimum.X, bounds.Maximum.X };
            float[] y = { bounds.Minimum.Y, bounds.Maximum.Y };
            float[] z = { bounds.Minimum.Z, bounds.Maximum.Z };
            foreach (float px in x)
            foreach (float py in y)
            foreach (float pz in z)
                yield return new Point3D(px, py, pz);
        }
    }
}

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    public sealed class DisplayShaderFilterEditorTool : IEditorCustomControlTool, IDisposable
    {
        private static bool s_environmentNoticeShown;
        private readonly EditorContext _context;
        private readonly DisplayShaderFilterEffect? _effect;
        private readonly string _saveDebounceKey = $"{nameof(DisplayShaderFilterEditorTool)}_{Guid.NewGuid():N}";
        private DisplayShaderFilterState? _persistenceState;
        private Action? _saveAction;
        private Effect? _previousEffect;
        private bool _effectAttached;
        private bool _isApplyingPersistenceState;
        private bool _disposed;
        private int _persistenceGeneration;
        private DisplayShaderFilterToolControl? _toolControl;
        private Settings.ImageViewSettingsWindow? _window;

        public DisplayShaderFilterEditorTool(EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            State = new DisplayShaderFilterState();
            State.CopyFrom(DisplayShaderFilterDefaultConfig.Current.State);
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            State.PropertyChanged += State_PropertyChanged;
            if (DisplayShaderFilterEnvironment.Current.CanUseShaderFilter)
            {
                _effect = new DisplayShaderFilterEffect();
                _effect.Apply(State);
            }

            UpdateEffectAttachment();
        }

        public DisplayShaderFilterState State { get; }
        public ICommand OpenSettingsCommand { get; }
        public event EventHandler? StateChanged;
        public bool HasExternalPersistence => _persistenceState != null;

        public void RestoreDefaults() => State.CopyFrom(DisplayShaderFilterDefaultConfig.Current.State);

        public void SaveAsDefault()
        {
            DisplayShaderFilterDefaultConfig defaults = DisplayShaderFilterDefaultConfig.Current;
            defaults.UpdateFrom(State);
            Settings.ImageSettingsPersistence.Save(defaults);
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => nameof(DisplayShaderFilterEditorTool);
        public int Order => 35;
        public object? Icon => null;
        public ICommand? Command => null;

        public FrameworkElement CreateToolControl()
        {
            _toolControl ??= new DisplayShaderFilterToolControl
            {
                DataContext = this
            };
            return _toolControl;
        }

        private void State_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _effect?.Apply(State);
            if (!_isApplyingPersistenceState)
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
                ScheduleSavePersistence();
            }

            if (e.PropertyName == nameof(DisplayShaderFilterState.IsEnabled))
            {
                if (State.IsEnabled)
                {
                    ShowEnvironmentNotice(false);
                }

                UpdateEffectAttachment();
            }
        }

        private void UpdateEffectAttachment()
        {
            if (State.IsEnabled && _effect != null)
            {
                if (!_effectAttached)
                {
                    _previousEffect = _context.DrawCanvas.Effect;
                    _effectAttached = true;
                }

                _context.DrawCanvas.Effect = _effect;
            }
            else
            {
                DetachEffect();
            }

            _context.ImageView.SchedulePixelValueOverlayRefresh();
        }

        private void DetachEffect()
        {
            if (!_effectAttached)
            {
                return;
            }

            if (_context.DrawCanvas.Effect == _effect)
            {
                _context.DrawCanvas.Effect = _previousEffect;
            }

            _previousEffect = null;
            _effectAttached = false;
        }

        public void OpenSettingsWindow()
        {
            if (!DisplayShaderFilterEnvironment.Current.CanUseShaderFilter)
            {
                ShowEnvironmentNotice(true);
                return;
            }

            ShowEnvironmentNotice(false);

            if (_window != null)
            {
                _window.Activate();
                return;
            }

            _window = new Settings.ImageViewSettingsWindow(_context.ImageView, Settings.ImageSettingsCategories.Filters)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            _window.Closed += (_, _) =>
            {
                _window = null;
            };
            _window.Show();
        }

        private void OpenSettings()
        {
            OpenSettingsWindow();
        }

        public void AttachPersistence(DisplayShaderFilterState state, Action? saveAction)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DebounceTimer.Cancel(_saveDebounceKey);
            _persistenceGeneration++;
            _persistenceState = state ?? DisplayShaderFilterDefaultConfig.Current.State;
            _saveAction = saveAction ?? DisplayShaderFilterDefaultConfig.SaveCurrent;
            _isApplyingPersistenceState = true;
            try
            {
                State.CopyFrom(_persistenceState);
                _effect?.Apply(State);
                UpdateEffectAttachment();
            }
            finally
            {
                _isApplyingPersistenceState = false;
            }
        }

        private void ScheduleSavePersistence()
        {
            if (_persistenceState == null || _disposed) return;
            int generation = _persistenceGeneration;
            DebounceTimer.AddOrResetTimerDispatcher(_saveDebounceKey, 600, () =>
            {
                if (!_disposed && generation == _persistenceGeneration) Save();
            });
        }

        public void Save()
        {
            if (_persistenceState == null || _disposed) return;
            DebounceTimer.Cancel(_saveDebounceKey);
            _persistenceState.CopyFrom(State);
            _saveAction?.Invoke();
        }

        private static void ShowEnvironmentNotice(bool force)
        {
            DisplayShaderFilterEnvironment environment = DisplayShaderFilterEnvironment.Current;
            if (!force && (!environment.ShouldShowNotice || s_environmentNoticeShown))
            {
                return;
            }

            s_environmentNoticeShown = true;
            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                environment.CreateNoticeText(),
                "Shader Filter Environment",
                MessageBoxButton.OK,
                environment.CanUseShaderFilter ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        public void Dispose()
        {
            if (_disposed) return;
            try
            {
                _window?.Close();
                Save();
            }
            finally
            {
                _disposed = true;
                _persistenceGeneration++;
                DebounceTimer.Cancel(_saveDebounceKey);
                State.PropertyChanged -= State_PropertyChanged;
                DetachEffect();
                GC.SuppressFinalize(this);
            }
        }
    }
}

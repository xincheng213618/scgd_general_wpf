using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Settings;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Effects;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    internal sealed class DisplayShaderFilterEditorTool : IEditorCustomControlTool, IDisplayShaderFilterService, IDisposable, IImageViewSettingProvider, IImageViewSettingPersistence
    {
        private static bool s_environmentNoticeShown;
        private readonly EditorContext _context;
        private readonly DisplayShaderFilterEffect? _effect;
        private readonly string _saveDebounceKey = $"{nameof(DisplayShaderFilterEditorTool)}_{Guid.NewGuid():N}";
        private DisplayShaderFilterState _persistenceState;
        private Action? _saveAction;
        private Effect? _previousEffect;
        private bool _effectAttached;
        private bool _isApplyingPersistenceState;
        private DisplayShaderFilterToolControl? _toolControl;
        private DisplayShaderFilterWindow? _window;

        public DisplayShaderFilterEditorTool(EditorContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _persistenceState = DisplayShaderFilterDefaultConfig.Current.State;
            _saveAction = DisplayShaderFilterDefaultConfig.SaveCurrent;
            State = new DisplayShaderFilterState();
            State.CopyFrom(_persistenceState);
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            State.PropertyChanged += State_PropertyChanged;
            if (DisplayShaderFilterEnvironment.Current.CanUseShaderFilter)
            {
                _effect = new DisplayShaderFilterEffect();
                _effect.Apply(State);
            }

            UpdateEffectAttachment();
            _context.RegisterService<IDisplayShaderFilterService>(this);
        }

        public DisplayShaderFilterState State { get; }
        public ICommand OpenSettingsCommand { get; }
        public event EventHandler? StateChanged;

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

            _window = new DisplayShaderFilterWindow(State)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            _window.Closed += (_, _) =>
            {
                Save();
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
            DebounceTimer.AddOrResetTimerDispatcher(_saveDebounceKey, 600, Save);
        }

        public void Save()
        {
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

        public IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView)
        {
            yield return new ImageViewSettingMetadata
            {
                Group = "Shader Filter",
                Order = 10,
                Scope = ImageViewSettingScope.CurrentView,
                Type = ImageViewSettingType.Class,
                Name = "Current shader filter",
                Description = "Display-only shader filter state for this ImageView.",
                Source = State,
            };

        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            Save();
            DisplayShaderFilterDefaultConfig.SaveCurrent();
        }

        public void Dispose()
        {
            _window?.Close();
            Save();
            State.PropertyChanged -= State_PropertyChanged;
            DetachEffect();
            _context.UnregisterService<IDisplayShaderFilterService>();
            GC.SuppressFinalize(this);
        }
    }
}

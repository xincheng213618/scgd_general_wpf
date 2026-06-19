using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    public sealed class DisplayShaderFilterDefaultConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static DisplayShaderFilterDefaultConfig? _current;
        private DisplayShaderFilterState _state = new();

        public static DisplayShaderFilterDefaultConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        var configBacked = ConfigService.Instance.GetRequiredService<DisplayShaderFilterDefaultConfig>();
                        lock (SyncLock)
                        {
                            _current = configBacked;
                            return _current;
                        }
                    }
                    catch
                    {
                    }
                }

                lock (SyncLock)
                {
                    _current ??= new DisplayShaderFilterDefaultConfig();
                    return _current;
                }
            }
        }

        public DisplayShaderFilterState State
        {
            get => _state;
            set
            {
                _state = value ?? new DisplayShaderFilterState();
                OnPropertyChanged();
            }
        }

        public void ApplyTo(DisplayShaderFilterState target)
        {
            target?.CopyFrom(State);
        }

        public void UpdateFrom(DisplayShaderFilterState source)
        {
            State.CopyFrom(source);
            OnPropertyChanged(nameof(State));
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<DisplayShaderFilterDefaultConfig>();
            }
            catch
            {
            }
        }
    }
}

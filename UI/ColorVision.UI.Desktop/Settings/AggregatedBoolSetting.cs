using ColorVision.Common.MVVM;
using System.Reflection;

namespace ColorVision.UI.Desktop.Settings
{
    internal sealed class AggregatedBoolSetting : ViewModelBase
    {
        private readonly List<AggregatedBoolSettingTarget> _targets;

        public AggregatedBoolSetting(IEnumerable<AggregatedBoolSettingTarget> targets)
        {
            _targets = targets.ToList();
        }

        public bool IsChecked
        {
            get => _targets.Any(target => target.GetValue());
            set
            {
                foreach (var target in _targets)
                {
                    target.SetValue(value);
                }

                OnPropertyChanged();
            }
        }
    }

    internal sealed class AggregatedBoolSettingTarget
    {
        public AggregatedBoolSettingTarget(object source, PropertyInfo property)
        {
            Source = source;
            Property = property;
        }

        public object Source { get; }
        public PropertyInfo Property { get; }

        public bool GetValue()
        {
            return Property.GetValue(Source) is bool value && value;
        }

        public void SetValue(bool value)
        {
            if (Property.CanWrite)
            {
                Property.SetValue(Source, value);
            }
        }
    }
}
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.ImageEditor.Settings
{
    /// <summary>Tracks only declared persistent settings, never the editor's runtime object graph.</summary>
    internal sealed class ImageSettingsSession : IDisposable
    {
        private sealed class Target(ImageViewSettingsEntry entry)
        {
            public ImageViewSettingsEntry Entry { get; } = entry;
            public string Baseline { get; set; } = Snapshot(entry.Source);
            public bool IsDirty => Baseline != Snapshot(Entry.Source);
        }

        private readonly List<Target> _targets;
        public event EventHandler? Changed;

        public ImageSettingsSession(IEnumerable<ImageViewSettingsEntry> entries)
        {
            _targets = entries.Where(entry => entry.Save != null && !entry.IsReadOnly && entry.Scope is ImageSettingsScope.Application or ImageSettingsScope.Defaults)
                .DistinctBy(entry => entry.Source, ReferenceEqualityComparer.Instance)
                .Select(entry => new Target(entry)).ToList();
            foreach (Target target in _targets)
                if (target.Entry.Source is INotifyPropertyChanged source) source.PropertyChanged += SourceChanged;
        }

        private static string Snapshot(object source) => JsonConvert.SerializeObject(source, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
        public bool HasChanges => _targets.Any(target => target.IsDirty);
        public void AcceptSaved(object? source)
        {
            foreach (Target target in _targets.Where(target => ReferenceEquals(target.Entry.Source, source)))
                target.Baseline = Snapshot(target.Entry.Source);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        private void SourceChanged(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

        public IReadOnlyList<string> SaveChanged()
        {
            List<string> errors = new();
            foreach (Target target in _targets)
            {
                if (!target.IsDirty) continue;
                try
                {
                    target.Entry.Save!();
                    target.Baseline = Snapshot(target.Entry.Source);
                }
                catch (Exception ex) { errors.Add($"{target.Entry.Title}: {ex.Message}"); }
            }
            Changed?.Invoke(this, EventArgs.Empty);
            return errors;
        }

        public void Dispose()
        {
            foreach (Target target in _targets)
                if (target.Entry.Source is INotifyPropertyChanged source) source.PropertyChanged -= SourceChanged;
        }
    }
}

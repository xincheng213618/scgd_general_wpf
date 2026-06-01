using ColorVision.ImageEditor.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Settings
{
    public partial class ImageViewContextSettingsView : UserControl, INotifyPropertyChanged
    {
        private readonly ImageView _imageView;

        public ImageViewContextSettingsView(ImageView imageView)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            Sections = new ObservableCollection<ImageViewPropertyScopeSectionViewModel>();
            InitializeComponent();
            DataContext = this;
            ReloadSections();
        }

        public ObservableCollection<ImageViewPropertyScopeSectionViewModel> Sections { get; }

        public bool HasEntries => Sections.Count > 0;

        public bool IsEmpty => Sections.Count == 0;

        public string SummaryText
        {
            get
            {
                int totalEntries = Sections.Sum(section => section.Entries.Count);
                if (totalEntries == 0)
                {
                    return Properties.Resources.Settings_NoPropertyContext;
                }

                return string.Format(Properties.Resources.Settings_PropertyContextSummary, totalEntries, Sections.Count);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void ReloadSections()
        {
            Sections.Clear();

            IEnumerable<IGrouping<ImageViewPropertyScope, ImageViewPropertyEntry>> groups = _imageView.Config
                .GetPropertyEntries()
                .OrderBy(entry => ImageViewConfig.GetScopeSortOrder(entry.Scope))
                .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                .GroupBy(entry => entry.Scope);

            foreach (IGrouping<ImageViewPropertyScope, ImageViewPropertyEntry> group in groups)
            {
                ObservableCollection<ImageViewPropertyEntryViewModel> entries = new(
                    group.Select(entry => new ImageViewPropertyEntryViewModel
                    {
                        Key = entry.Key,
                        Value = ImageViewConfig.FormatPropertyValue(entry.Value),
                        Owner = entry.Owner,
                        Description = entry.Description,
                    }));

                Sections.Add(new ImageViewPropertyScopeSectionViewModel
                {
                    ScopeName = ImageViewConfig.GetScopeDisplayName(group.Key),
                    ScopeDescription = ImageViewConfig.GetScopeDescription(group.Key),
                    Entries = entries,
                });
            }

            RaisePropertyChanged(nameof(HasEntries));
            RaisePropertyChanged(nameof(IsEmpty));
            RaisePropertyChanged(nameof(SummaryText));
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ReloadSections();
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class ImageViewPropertyScopeSectionViewModel
    {
        public string ScopeName { get; set; } = string.Empty;

        public string ScopeDescription { get; set; } = string.Empty;

        public ObservableCollection<ImageViewPropertyEntryViewModel> Entries { get; set; } = new();

        public string CountText => string.Format(Properties.Resources.Settings_EntryCount, Entries.Count);
    }

    public sealed class ImageViewPropertyEntryViewModel
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string? Owner { get; set; }

        public string? Description { get; set; }

        public bool HasOwner => !string.IsNullOrWhiteSpace(Owner);

        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    }
}
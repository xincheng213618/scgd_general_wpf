using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DisplayAlgorithmFileAttribute : Attribute
    {
        public string Filter { get; }

        public DisplayAlgorithmFileAttribute(string filter = "")
        {
            Filter = filter;
        }
    }

    public abstract class DisplayAlgorithmConfigBase : ViewModelBase
    {
        [DisplayAlgorithmFile]
        [Display(Order = -1000)]
        public string ImageFilePath
        {
            get => _imageFilePath;
            set
            {
                _imageFilePath = value;
                OnPropertyChanged();
            }
        }
        private string _imageFilePath = string.Empty;
    }

    public class SingleTemplateDisplayAlgorithmConfig : DisplayAlgorithmConfigBase
    {
        [Display(Order = 0)]
        public DisplayAlgorithmTemplateSelection Template { get; set; }

        public SingleTemplateDisplayAlgorithmConfig(DisplayAlgorithmTemplateSelection template)
        {
            Template = template;
        }
    }

    public class DualTemplateDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [Display(Order = 10)]
        public DisplayAlgorithmTemplateSelection SecondaryTemplate { get; set; }

        public DualTemplateDisplayAlgorithmConfig(
            DisplayAlgorithmTemplateSelection template,
            DisplayAlgorithmTemplateSelection secondaryTemplate)
            : base(template)
        {
            SecondaryTemplate = secondaryTemplate;
        }
    }

    public class CieFileDisplayAlgorithmConfig : SingleTemplateDisplayAlgorithmConfig
    {
        [DisplayName("CIE文件")]
        [DisplayAlgorithmFile]
        public string CIEFileName { get; set; } = string.Empty;

        public CieFileDisplayAlgorithmConfig(DisplayAlgorithmTemplateSelection template)
            : base(template)
        {
        }
    }

    public sealed class DisplayAlgorithmTemplateSelection : ViewModelBase
    {
        private readonly IEnumerable _itemsSource;
        private readonly Func<int>? _selectedIndexGetter;
        private readonly Action<int>? _selectedIndexSetter;
        private readonly int _editorIndexOffset;

        [Browsable(false)]
        public string DisplayName { get; }

        [Browsable(false)]
        public ITemplate Template { get; }

        [Browsable(false)]
        public string ValidationMessage { get; }

        [Browsable(false)]
        public IEnumerable ItemsSource => _itemsSource;

        [Browsable(false)]
        public ICommand EditCommand { get; }

        public int SelectedIndex
        {
            get => _selectedIndexGetter?.Invoke() ?? _selectedIndex;
            set
            {
                _selectedIndex = value;
                _selectedIndexSetter?.Invoke(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedValue));
                OnPropertyChanged(nameof(SelectedName));
            }
        }
        private int _selectedIndex;

        [Browsable(false)]
        public object? SelectedValue => GetSelectedValue();

        [Browsable(false)]
        public string SelectedName => GetSelectedName();

        public DisplayAlgorithmTemplateSelection(
            string displayName,
            ITemplate template,
            string validationMessage,
            Func<IEnumerable>? itemsSource = null,
            int selectedIndex = 0,
            Func<int>? selectedIndexGetter = null,
            Action<int>? selectedIndexSetter = null,
            int editorIndexOffset = 0)
        {
            DisplayName = displayName;
            Template = template;
            ValidationMessage = validationMessage;
            _itemsSource = itemsSource?.Invoke() ?? template.ItemsSource;
            _selectedIndex = selectedIndex;
            _selectedIndexGetter = selectedIndexGetter;
            _selectedIndexSetter = selectedIndexSetter;
            _editorIndexOffset = editorIndexOffset;
            EditCommand = new RelayCommand(_ => OpenTemplateEditor());
        }

        public bool TryGetValue<T>(out T value)
        {
            if (SelectedValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            value = default!;
            return false;
        }

        public bool IsSelectionValid()
        {
            return SelectedIndex >= 0 && SelectedIndex < ItemsSource.Cast<object>().Count();
        }

        private object? GetSelectedValue()
        {
            object? selectedItem = ItemsSource.Cast<object>().ElementAtOrDefault(SelectedIndex);
            if (selectedItem == null)
            {
                return null;
            }

            return selectedItem.GetType().GetProperty("Value")?.GetValue(selectedItem) ?? selectedItem;
        }

        private string GetSelectedName()
        {
            object? selectedItem = ItemsSource.Cast<object>().ElementAtOrDefault(SelectedIndex);
            if (selectedItem == null)
            {
                return string.Empty;
            }

            return selectedItem.GetType().GetProperty("Key")?.GetValue(selectedItem)?.ToString()
                ?? selectedItem.ToString()
                ?? string.Empty;
        }

        private void OpenTemplateEditor()
        {
            new TemplateEditorWindow(Template, SelectedIndex + _editorIndexOffset)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}

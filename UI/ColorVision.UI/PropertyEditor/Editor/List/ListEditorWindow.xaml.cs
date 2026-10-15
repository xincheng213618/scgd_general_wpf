#pragma warning disable CA1863
using Newtonsoft.Json;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.PropertyEditor.Editor.List
{
    public partial class ListEditorWindow : Window
    {
        private readonly Type _elementType;
        private readonly Type? _itemEditorType;
        private readonly System.Collections.IList _items;
        private readonly System.Collections.IList _originalItems;
        private bool UsesInlineEditor => _elementType == typeof(string);
        
        public bool DialogResultValue { get; private set; }

        public class ListItemViewModel : INotifyPropertyChanged
        {
            public int Index { get; set; }
            private object? _value;
            public object? Value
            {
                get => _value;
                set
                {
                    if (Equals(_value, value)) return;
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextValue)));
                }
            }

            // Keep typing in the working list even when a toolbar action recreates the rows.
            [PropertyEditorType(UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged)]
            public string? TextValue { get => Value as string; set => Value = value; }

            public DockPanel? InlineEditor { get; internal set; }
            public object Content => (object?)InlineEditor ?? DisplayValue;
            public event PropertyChangedEventHandler? PropertyChanged;

            public Type? Type { get; set; }
            
            public string DisplayValue
            {
                get
                {
                    if (Value == null)
                        return "(null)";
                    
                    // Special handling for nested lists
                    if (Value is System.Collections.IList list)
                    {
                        return string.Format(Properties.Resources.ListEditor_ListCount, list.Count);
                    }
                    var valueType = Type ?? Value.GetType();
                    if (valueType.IsClass && valueType != typeof(string))
                    {
                        return JsonConvert.SerializeObject(Value);
                    }
                    return Value.ToString() ?? string.Empty;
                }
            }
        }
        public ListEditorWindow(IList items, Type elementType, Type? itemEditorType = null)
        {
            InitializeComponent();
            ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(this);
            _elementType = elementType;
            _itemEditorType = itemEditorType;
            _originalItems = items;
            
            // Create a working copy
            var listType = typeof(List<>).MakeGenericType(elementType);
            _items = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in items)
            {
                _items.Add(item);
            }
            EditButton.Visibility = UsesInlineEditor ? Visibility.Collapsed : Visibility.Visible;
            RefreshList();
        }

        private void RefreshList()
        {
            var viewModels = new List<ListItemViewModel>();
            for (int i = 0; i < _items.Count; i++)
            {
                var viewModel = new ListItemViewModel
                {
                    Index = i,
                    Type = _elementType,     
                    Value = _items[i]
                };
                if (UsesInlineEditor)
                {
                    viewModel.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(ListItemViewModel.Value))
                            _items[viewModel.Index] = viewModel.Value;
                    };
                    viewModel.InlineEditor = CreateInlineEditor(viewModel);
                }
                viewModels.Add(viewModel);
            }
            ItemsListBox.ItemsSource = viewModels;
            UpdateButtonStates();
        }

        private DockPanel CreateInlineEditor(ListItemViewModel item)
        {
            var property = typeof(ListItemViewModel).GetProperty(nameof(ListItemViewModel.TextValue))!;
            DockPanel panel;
            try
            {
                var editor = PropertyEditorHelper.GetOrCreateEditor(_itemEditorType ?? typeof(TextboxPropertiesEditor));
                panel = editor.GenProperties(property, item);
            }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(typeof(ListEditorWindow)).Warn("Unable to create the collection item editor; using a text editor.", ex);
                panel = PropertyEditorHelper.GetOrCreateEditor<TextboxPropertiesEditor>().GenProperties(property, item);
            }

            var label = panel.Children.OfType<TextBlock>().FirstOrDefault();
            if (label != null) panel.Children.Remove(label);
            foreach (var textBox in panel.Children.OfType<TextBox>())
            {
                textBox.Margin = new Thickness(0);
                AutomationProperties.SetName(textBox, Properties.Resources.ListEditor_Value);
            }
            return panel;
        }

        private void Item_PreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // Editing or browsing a row must also select it for move/delete actions.
            if (sender is ListBoxItem row && !row.IsSelected)
                ItemsListBox.SelectedItem = row.DataContext;
        }

        private void SelectItem(int index, bool focusEditor = false)
        {
            ItemsListBox.SelectedIndex = index;
            if (ItemsListBox.SelectedItem is not ListItemViewModel item) return;
            ItemsListBox.ScrollIntoView(item);
            if (focusEditor)
            {
                ItemsListBox.UpdateLayout();
                var textBox = item.InlineEditor?.Children.OfType<TextBox>().FirstOrDefault();
                textBox?.Focus();
                textBox?.SelectAll();
            }
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = ItemsListBox.SelectedItems.Count > 0;
            bool hasSingleSelection = ItemsListBox.SelectedItems.Count == 1;
            EditButton.IsEnabled = hasSingleSelection;
            DeleteButton.IsEnabled = hasSelection;
            DeleteAllButton.IsEnabled = _items.Count > 0;
            MoveUpButton.IsEnabled = hasSingleSelection && ItemsListBox.SelectedIndex > 0;
            MoveDownButton.IsEnabled = hasSingleSelection && ItemsListBox.SelectedIndex < _items.Count - 1;
        }

        private void ItemsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void ItemsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!UsesInlineEditor && ItemsListBox.SelectedIndex >= 0)
            {
                EditButton_Click(sender, e);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var defaultValue = GetDefaultValue(_elementType);
            if (UsesInlineEditor)
            {
                _items.Add(defaultValue);
                RefreshList();
                SelectItem(_items.Count - 1, focusEditor: true);
                return;
            }
            var editor = new ListItemEditorWindow(_elementType, defaultValue, _itemEditorType);
            editor.Owner = this;
            
            if (editor.ShowDialog() == true)
            {
                var convertedValue = PropertyEditorHelper.ConvertToTargetType(editor.EditedValue, _elementType);
                _items.Add(convertedValue);
                RefreshList();
                SelectItem(_items.Count - 1);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListBox.SelectedIndex < 0) return;
            int index = ItemsListBox.SelectedIndex;
            if (UsesInlineEditor)
            {
                SelectItem(index, focusEditor: true);
                return;
            }

            var currentValue = _items[index];
            var editor = new ListItemEditorWindow(_elementType, currentValue, _itemEditorType);
            editor.Owner = this;
            
            if (editor.ShowDialog() == true)
            {
                var convertedValue = PropertyEditorHelper.ConvertToTargetType(editor.EditedValue, _elementType);
                _items[index] = convertedValue;
                RefreshList();
                SelectItem(index);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListBox.SelectedItems.Count == 0) return;

            int selectedCount = ItemsListBox.SelectedItems.Count;
            var result = MessageBox.Show(string.Format(Properties.Resources.ListEditor_ConfirmDeleteSelected, selectedCount), Properties.Resources.ListEditor_ConfirmDeleteTitle,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // Get selected indices in descending order to avoid index shifting issues
                var selectedIndices = ItemsListBox.SelectedItems
                    .Cast<ListItemViewModel>()
                    .Select(item => item.Index)
                    .OrderByDescending(i => i)
                    .ToList();

                foreach (var index in selectedIndices)
                {
                    _items.RemoveAt(index);
                }
                RefreshList();
                
                // Update button states after deletion
                UpdateButtonStates();
            }
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;

            var result = MessageBox.Show(string.Format(Properties.Resources.ListEditor_ConfirmDeleteAll, _items.Count), Properties.Resources.ListEditor_ConfirmDeleteAllTitle, 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                _items.Clear();
                RefreshList();
                UpdateButtonStates();
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            int index = ItemsListBox.SelectedIndex;
            if (index <= 0) return;

            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(index - 1, item);
            RefreshList();
            SelectItem(index - 1);
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            int index = ItemsListBox.SelectedIndex;
            if (index < 0 || index >= _items.Count - 1) return;

            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(index + 1, item);
            RefreshList();
            SelectItem(index + 1);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Copy items back to original list
            _originalItems.Clear();
            foreach (var item in _items)
            {
                _originalItems.Add(item);
            }
            
            DialogResultValue = true;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResultValue = false;
            DialogResult = false;
            Close();
        }

        private static object? GetDefaultValue(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            else if (type == typeof(string))
            {
                return string.Empty;
            }
            return null;
        }
    }
}

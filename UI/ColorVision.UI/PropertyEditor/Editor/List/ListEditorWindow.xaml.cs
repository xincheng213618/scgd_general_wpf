using Newtonsoft.Json;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.PropertyEditor.Editor.List
{
    public partial class ListEditorWindow : Window
    {
        private readonly Type _elementType;
        private readonly System.Collections.IList _items;
        private readonly System.Collections.IList _originalItems;
        
        public bool DialogResultValue { get; private set; }

        public class ListItemViewModel
        {
            public int Index { get; set; }
            public object? Value { get; set; }

            public Type Type { get; set; }
            
            public string DisplayValue
            {
                get
                {
                    if (Value == null)
                        return "(null)";
                    
                    // Special handling for nested lists
                    if (Value is System.Collections.IList list)
                    {
                        return $"[列表: {list.Count} 项]";
                    }
                    if (Type.IsClass)
                    {
                        return JsonConvert.SerializeObject(Value);
                    }
                    return Value.ToString() ?? string.Empty;
                }
            }
        }
        public ListEditorWindow(IList items, Type elementType)
        {
            InitializeComponent();
            _elementType = elementType;
            _originalItems = items;
            
            // Create a working copy
            var listType = typeof(List<>).MakeGenericType(elementType);
            _items = (IList)Activator.CreateInstance(listType)!;
            foreach (var item in items)
            {
                _items.Add(item);
            }
            RefreshListView();
        }

        private void RefreshListView()
        {
            var viewModels = new List<ListItemViewModel>();
            for (int i = 0; i < _items.Count; i++)
            {
                viewModels.Add(new ListItemViewModel
                {
                    Index = i,
                    Type = _elementType,     
                    Value = _items[i]

                });

            }
            ItemsListView.ItemsSource = viewModels;
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = ItemsListView.SelectedItems.Count > 0;
            bool hasSingleSelection = ItemsListView.SelectedItems.Count == 1;
            EditButton.IsEnabled = hasSingleSelection;
            DeleteButton.IsEnabled = hasSelection;
            DeleteAllButton.IsEnabled = _items.Count > 0;
            MoveUpButton.IsEnabled = hasSingleSelection && ItemsListView.SelectedIndex > 0;
            MoveDownButton.IsEnabled = hasSingleSelection && ItemsListView.SelectedIndex < _items.Count - 1;
        }

        private void ItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates();
        }

        private void ItemsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemsListView.SelectedIndex >= 0)
            {
                EditButton_Click(sender, e);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var defaultValue = GetDefaultValue(_elementType);
            var editor = new ListItemEditorWindow(_elementType, defaultValue);
            editor.Owner = this;
            
            if (editor.ShowDialog() == true)
            {
                var convertedValue = PropertyEditorHelper.ConvertToTargetType(editor.EditedValue, _elementType);
                _items.Add(convertedValue);
                RefreshListView();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedIndex < 0) return;

            var currentValue = _items[ItemsListView.SelectedIndex];
            var editor = new ListItemEditorWindow(_elementType, currentValue);
            editor.Owner = this;
            
            if (editor.ShowDialog() == true)
            {
                var convertedValue = PropertyEditorHelper.ConvertToTargetType(editor.EditedValue, _elementType);
                _items[ItemsListView.SelectedIndex] = convertedValue;
                RefreshListView();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedItems.Count == 0) return;

            int selectedCount = ItemsListView.SelectedItems.Count;
            var result = MessageBox.Show($"确定要删除选中的 {selectedCount} 项吗？", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // Get selected indices in descending order to avoid index shifting issues
                var selectedIndices = ItemsListView.SelectedItems
                    .Cast<ListItemViewModel>()
                    .Select(item => item.Index)
                    .OrderByDescending(i => i)
                    .ToList();

                foreach (var index in selectedIndices)
                {
                    _items.RemoveAt(index);
                }
                RefreshListView();
                
                // Update button states after deletion
                UpdateButtonStates();
            }
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;

            var result = MessageBox.Show($"确定要删除全部 {_items.Count} 项吗？", "确认全部删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                _items.Clear();
                RefreshListView();
                UpdateButtonStates();
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            int index = ItemsListView.SelectedIndex;
            if (index <= 0) return;

            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(index - 1, item);
            RefreshListView();
            ItemsListView.SelectedIndex = index - 1;
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            int index = ItemsListView.SelectedIndex;
            if (index < 0 || index >= _items.Count - 1) return;

            var item = _items[index];
            _items.RemoveAt(index);
            _items.Insert(index + 1, item);
            RefreshListView();
            ItemsListView.SelectedIndex = index + 1;
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

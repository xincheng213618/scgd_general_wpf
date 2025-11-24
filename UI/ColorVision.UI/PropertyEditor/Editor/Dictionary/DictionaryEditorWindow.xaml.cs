using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.PropertyEditor.Editor.Dictionary
{
    public partial class DictionaryEditorWindow : Window
    {
        private readonly Type _keyType;
        private readonly Type _valueType;
        private readonly System.Collections.IDictionary _items;
        private readonly System.Collections.IDictionary _originalItems;
        
        public bool DialogResultValue { get; private set; }

        public class DictionaryItemViewModel
        {
            public object? Key { get; set; }
            public object? Value { get; set; }
            
            public string DisplayKey
            {
                get
                {
                    if (Key == null)
                        return "(null)";
                    return Key.ToString() ?? string.Empty;
                }
            }
            
            public string DisplayValue
            {
                get
                {
                    if (Value == null)
                        return "(null)";
                    
                    // Special handling for nested collections
                    if (Value is System.Collections.ICollection collection)
                    {
                        return $"[集合: {collection.Count} 项]";
                    }
                    
                    return Value.ToString() ?? string.Empty;
                }
            }
        }

        public DictionaryEditorWindow(IDictionary items, Type keyType, Type valueType)
        {
            InitializeComponent();
            _keyType = keyType;
            _valueType = valueType;
            _originalItems = items;
            
            // Create a working copy
            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            _items = (IDictionary)Activator.CreateInstance(dictType)!;
            foreach (DictionaryEntry entry in items)
            {
                _items.Add(entry.Key, entry.Value);
            }

            RefreshListView();
        }

        private void RefreshListView()
        {
            var viewModels = new List<DictionaryItemViewModel>();
            foreach (DictionaryEntry entry in _items)
            {
                viewModels.Add(new DictionaryItemViewModel
                {
                    Key = entry.Key,
                    Value = entry.Value
                });
            }
            ItemsListView.ItemsSource = viewModels;
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = ItemsListView.SelectedIndex >= 0;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
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
            var defaultKey = GetDefaultValue(_keyType);
            var defaultValue = GetDefaultValue(_valueType);
            
            var editor = new DictionaryItemEditorWindow(_keyType, _valueType, defaultKey, defaultValue, _items.Keys);
            editor.Owner = this;
            
            if (editor.ShowDialog() == true)
            {
                _items.Add(editor.EditedKey!, editor.EditedValue);
                RefreshListView();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedIndex < 0) return;

            var selectedItem = (DictionaryItemViewModel)ItemsListView.SelectedItem;
            var currentKey = selectedItem.Key;
            var currentValue = selectedItem.Value;
            
            // Exclude the current key from the existing keys (since we're editing it)
            var existingKeys = new List<object>();
            foreach (var key in _items.Keys)
            {
                if (!Equals(key, currentKey))
                    existingKeys.Add(key);
            }
            
            var editor = new DictionaryItemEditorWindow(_keyType, _valueType, currentKey, currentValue, existingKeys);
            editor.Owner = this;
            
            if (editor.ShowDialog() == true)
            {
                // Remove old entry and add new one
                _items.Remove(currentKey!);
                _items.Add(editor.EditedKey!, editor.EditedValue);
                RefreshListView();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemsListView.SelectedIndex < 0) return;

            var result = MessageBox.Show("确定要删除选中的项吗？", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                var selectedItem = (DictionaryItemViewModel)ItemsListView.SelectedItem;
                _items.Remove(selectedItem.Key!);
                RefreshListView();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Copy items back to original dictionary
            _originalItems.Clear();
            foreach (DictionaryEntry entry in _items)
            {
                _originalItems.Add(entry.Key, entry.Value);
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

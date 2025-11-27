using Newtonsoft.Json;
using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        }

        private void UpdateButtonStates()
        {
            bool hasSelection = ItemsListView.SelectedIndex >= 0;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;
            MoveUpButton.IsEnabled = hasSelection && ItemsListView.SelectedIndex > 0;
            MoveDownButton.IsEnabled = hasSelection && ItemsListView.SelectedIndex < _items.Count - 1;
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
            if (ItemsListView.SelectedIndex < 0) return;

            var result = MessageBox.Show("确定要删除选中的项吗？", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                int selectedIndex = ItemsListView.SelectedIndex;
                _items.RemoveAt(selectedIndex);
                RefreshListView();
                
                // Restore selection
                if (_items.Count > 0)
                {
                    ItemsListView.SelectedIndex = Math.Min(selectedIndex, _items.Count - 1);
                }
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

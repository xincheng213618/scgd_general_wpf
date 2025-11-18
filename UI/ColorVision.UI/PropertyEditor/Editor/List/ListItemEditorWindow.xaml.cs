using ColorVision.Common.Utilities;
using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.UI.PropertyEditor.Editor.List
{
    public partial class ListItemEditorWindow : Window
    {
        private readonly Type _elementType;
        private object? _editedValue;
        private Control? _editorControl;

        public object? EditedValue => _editedValue;

        public ListItemEditorWindow(Type elementType, object? initialValue)
        {
            InitializeComponent();
            _elementType = elementType;
            _editedValue = initialValue;

            CreateEditor();
        }

        private void CreateEditor()
        {
            var label = new TextBlock
            {
                Text = "值:",
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.Bold
            };
            EditorPanel.Children.Add(label);

            if (_elementType == typeof(string))
            {
                CreateStringEditor();
            }
            else if (_elementType.IsEnum)
            {
                CreateEnumEditor();
            }
            else if (IsNumericType(_elementType))
            {
                CreateNumericEditor();
            }
            else
            {
                CreateTextBoxEditor();
            }
        }

        private void CreateStringEditor()
        {
            var dockPanel = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

            var textBox = new TextBox
            {
                Text = _editedValue?.ToString() ?? string.Empty,
                Style = PropertyEditorHelper.TextBoxSmallStyle,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _editorControl = textBox;

            // File selection button
            var selectFileBtn = new Button
            {
                Content = "选择文件",
                Margin = new Thickness(5, 0, 0, 0),
                Width = 80
            };
            selectFileBtn.Click += (s, e) =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                var path = textBox.Text;
#if NET8_0
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    ofd.DefaultDirectory = Directory.GetDirectoryRoot(path);
                }
#endif
                if (ofd.ShowDialog() == true)
                {
                    textBox.Text = ofd.FileName;
                }
            };

            // Folder selection button
            var selectFolderBtn = new Button
            {
                Content = "选择文件夹",
                Margin = new Thickness(5, 0, 0, 0),
                Width = 80
            };
            selectFolderBtn.Click += (s, e) =>
            {
                using var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    SelectedPath = textBox.Text ?? string.Empty
                };
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK &&
                    !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                {
                    textBox.Text = folderDialog.SelectedPath;
                }
            };

            // Open folder button
            var openFolderBtn = new Button
            {
                Content = "🗁",
                Margin = new Thickness(5, 0, 0, 0),
                Width = 30,
                ToolTip = "打开文件夹"
            };
            openFolderBtn.Click += (s, e) =>
            {
                var path = textBox.Text;
                if (!string.IsNullOrWhiteSpace(path))
                    PlatformHelper.OpenFolder(path);
            };

            DockPanel.SetDock(selectFileBtn, Dock.Right);
            DockPanel.SetDock(selectFolderBtn, Dock.Right);
            DockPanel.SetDock(openFolderBtn, Dock.Right);
            
            dockPanel.Children.Add(openFolderBtn);
            dockPanel.Children.Add(selectFolderBtn);
            dockPanel.Children.Add(selectFileBtn);
            dockPanel.Children.Add(textBox);

            EditorPanel.Children.Add(dockPanel);
        }

        private void CreateEnumEditor()
        {
            var comboBox = new ComboBox
            {
                Style = PropertyEditorHelper.ComboBoxSmallStyle,
                ItemsSource = Enum.GetValues(_elementType),
                SelectedItem = _editedValue
            };
            _editorControl = comboBox;

            EditorPanel.Children.Add(comboBox);
        }

        private void CreateNumericEditor()
        {
            var textBox = new TextBox
            {
                Text = _editedValue?.ToString() ?? "0",
                Style = PropertyEditorHelper.TextBoxSmallStyle
            };
            _editorControl = textBox;

            if (_elementType == typeof(float) || _elementType == typeof(double))
            {
                textBox.ToolTip = "输入数值，例如: 1.23";
            }
            else
            {
                textBox.ToolTip = "输入整数";
            }

            EditorPanel.Children.Add(textBox);
        }

        private void CreateTextBoxEditor()
        {
            var textBox = new TextBox
            {
                Text = _editedValue?.ToString() ?? string.Empty,
                Style = PropertyEditorHelper.TextBoxSmallStyle
            };
            _editorControl = textBox;

            EditorPanel.Children.Add(textBox);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_editorControl is TextBox textBox)
                {
                    _editedValue = ConvertValue(textBox.Text, _elementType);
                }
                else if (_editorControl is ComboBox comboBox)
                {
                    _editedValue = comboBox.SelectedItem;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"输入值无效: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static object? ConvertValue(string input, Type targetType)
        {
            if (targetType == typeof(string))
                return input;

            if (string.IsNullOrWhiteSpace(input))
            {
                if (targetType.IsValueType)
                    return Activator.CreateInstance(targetType);
                return null;
            }

            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (targetType == typeof(int))
                return int.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return long.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(short))
                return short.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(byte))
                return byte.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(uint))
                return uint.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(ulong))
                return ulong.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(ushort))
                return ushort.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(sbyte))
                return sbyte.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return float.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return double.Parse(input, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return decimal.Parse(input, CultureInfo.InvariantCulture);

            return Convert.ChangeType(input, targetType, CultureInfo.InvariantCulture);
        }

        private static bool IsNumericType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }
    }
}

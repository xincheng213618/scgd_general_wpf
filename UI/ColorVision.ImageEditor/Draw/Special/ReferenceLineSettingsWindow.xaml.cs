using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Special
{
    public partial class ReferenceLineSettingsWindow : Window
    {
        private readonly ReferenceLine _referenceLine;
        private bool _loading;

        public ReferenceLineSettingsWindow(ReferenceLine referenceLine)
        {
            _referenceLine = referenceLine ?? throw new ArgumentNullException(nameof(referenceLine));
            _loading = true;
            InitializeComponent();
            LoadValues();
            _loading = false;
            UpdateSections();
        }

        private ReferenceLineParam Attribute => _referenceLine.Attribute;

        private void LoadValues()
        {
            SelectComboItem(ModeComboBox, Attribute.Mode);
            SelectComboItem(MaskShapeComboBox, Attribute.MaskShape);

            LockCheckBox.IsChecked = _referenceLine.IsLocked;
            AngleTextBox.Text = Format(Attribute.Angle);
            LineWidthTextBox.Text = Format(Attribute.LineWidth);
            MaskSizeTextBox.Text = Format(Attribute.MaskSize);
            MaskOpacitySlider.Value = Attribute.MaskOpacity;

            UsePhysicalUnitCheckBox.IsChecked = DefalutTextAttribute.Defalut.IsUsePhysicalUnit;
            PhysicalLengthTextBox.Text = Format(DefalutTextAttribute.Defalut.ActualLength);
            PhysicalUnitTextBox.Text = DefalutTextAttribute.Defalut.PhysicalUnit;
        }

        private void Apply()
        {
            if (_loading) return;

            Attribute.Mode = SelectedTag(ModeComboBox, Attribute.Mode);
            Attribute.MaskShape = SelectedTag(MaskShapeComboBox, Attribute.MaskShape);
            Attribute.Angle = ReadDouble(AngleTextBox, Attribute.Angle);
            Attribute.LineWidth = ReadDouble(LineWidthTextBox, Attribute.LineWidth, 0.1);
            Attribute.MaskSize = ReadDouble(MaskSizeTextBox, Attribute.MaskSize, 1);
            Attribute.MaskOpacity = (byte)Math.Clamp((int)Math.Round(MaskOpacitySlider.Value), 0, 255);
            _referenceLine.IsLocked = LockCheckBox.IsChecked == true;

            DefalutTextAttribute.Defalut.IsUsePhysicalUnit = UsePhysicalUnitCheckBox.IsChecked == true;
            DefalutTextAttribute.Defalut.ActualLength = ReadDouble(PhysicalLengthTextBox, DefalutTextAttribute.Defalut.ActualLength, 0.0001);
            DefalutTextAttribute.Defalut.PhysicalUnit = string.IsNullOrWhiteSpace(PhysicalUnitTextBox.Text) ? "Px" : PhysicalUnitTextBox.Text.Trim();

            Attribute.Pen = new Pen(Attribute.Brush, Attribute.LineWidth / Math.Max(_referenceLine.Ratio, 0.0001));
            _referenceLine.Render();
            UpdateSections();
        }

        private void UpdateSections()
        {
            ReferenceLineMode mode = SelectedTag(ModeComboBox, Attribute.Mode);
            AngleRow.Visibility = mode == ReferenceLineMode.GridLines ? Visibility.Collapsed : Visibility.Visible;
            GridSection.Visibility = mode == ReferenceLineMode.GridLines ? Visibility.Visible : Visibility.Collapsed;
            MaskSection.Visibility = mode == ReferenceLineMode.CrossMask ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            Apply();
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Apply();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            Apply();
            e.Handled = true;
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string colorText } && ColorConverter.ConvertFromString(colorText) is Color color)
            {
                Attribute.Brush = new SolidColorBrush(color);
                Apply();
            }
        }

        private void ResetCenter_Click(object sender, RoutedEventArgs e)
        {
            _referenceLine.RMouseDownP = new Point(_referenceLine.ActualWidth / 2, _referenceLine.ActualHeight / 2);
            Attribute.Angle = 0;
            _referenceLine.PointLen = new Vector();
            AngleTextBox.Text = Format(Attribute.Angle);
            _referenceLine.Render();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static void SelectComboItem(ComboBox comboBox, object value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (!Equals(item.Tag, value)) continue;
                comboBox.SelectedItem = item;
                return;
            }

            comboBox.SelectedIndex = 0;
        }

        private static T SelectedTag<T>(ComboBox comboBox, T fallback)
        {
            return comboBox.SelectedItem is ComboBoxItem item && item.Tag is T value ? value : fallback;
        }

        private static double ReadDouble(TextBox textBox, double fallback, double minimum = double.NegativeInfinity)
        {
            string text = textBox.Text.Trim();
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value)
                && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                value = fallback;
            }

            value = Math.Max(minimum, value);
            textBox.Text = Format(value);
            return value;
        }

        private static string Format(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }
    }
}

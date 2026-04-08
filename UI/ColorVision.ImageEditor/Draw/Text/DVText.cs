using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class TextProperties : BaseProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();
        public bool IsShowText { get; set; } = true;

        [Category("Text"), DisplayName("文本")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("字体大小")]
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("颜色"), JsonIgnore]
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("字体"), JsonIgnore]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("FontStyle"), JsonIgnore]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; OnPropertyChanged(); } }
        [Category("Text"), DisplayName("FontWeight"), JsonIgnore]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; OnPropertyChanged(); } }
        [Category("Text"), DisplayName("FontStretch"), JsonIgnore]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("FlowDirection"), JsonIgnore]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; OnPropertyChanged(); } }

        [Category("Text"), DisplayName("位置")]
        public Point Position { get => _Position; set { if (_Position == value) return; _Position = value; OnPropertyChanged(); } }
        private Point _Position = new Point(50,50);

        [Browsable(false)]
        public Rect Rect { get => _Rect; set { _Rect = value; OnPropertyChanged(); } }
        private Rect _Rect = new Rect(50,50,0,0);

        [Browsable(false), JsonIgnore]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen = new Pen(Brushes.Red,1);

        [Category("Text"), DisplayName("背景"), JsonIgnore]
        public Brush Background { get => _Background; set { _Background = value; OnPropertyChanged(); } }
        private Brush _Background = Brushes.Transparent;

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        [Browsable(false)]
        public bool IsEditing { get => _IsEditing; set { _IsEditing = value; OnPropertyChanged(); } }
        private bool _IsEditing = false;
    }

    public class DVText : DrawingVisualBase<TextProperties>, IDrawingVisual, IEditableDrawingVisual
    {
        public TextAttribute TextAttribute => Attribute.TextAttribute;

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        // 编辑时使用的 TextBox
        private TextBox? _editTextBox;
        private bool _isEditing = false;

        public DVText()
        {
            Attribute = new TextProperties();
            Attribute.Text = "请在这里输入";
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10; // 与其它图元保持一致缩放策略
            Attribute.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(TextProperties.Rect))
                    Render();
            };
        }
        public DVText(TextProperties textProperties)
        {
            Attribute = textProperties;
            if (Attribute.FontSize <= 0)
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName != nameof(TextProperties.Rect))
                    Render();
            };
        }

        public override void Render()
        {
            // 如果处于编辑模式，不渲染 DrawingVisual
            if (_isEditing) return;

            using DrawingContext dc = RenderOpen();
            FormattedText formattedText = new(
                TextAttribute.Text,
                CultureInfo.CurrentCulture,
                TextAttribute.FlowDirection,
                new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                TextAttribute.FontSize,
                TextAttribute.Brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            // 更新 Rect 以包含文本实际尺寸
            var textWidth = formattedText.Width;
            var textHeight = formattedText.Height;
            Attribute.Rect = new Rect(Attribute.Position.X, Attribute.Position.Y, textWidth, textHeight);

            if (Attribute.Background != null && Attribute.Background != Brushes.Transparent)
            {
                dc.DrawRectangle(Attribute.Background, null, Attribute.Rect);
            }
            dc.DrawText(formattedText, Attribute.Position);
        }

        public override Rect GetRect() => Attribute.Rect;

        public override void SetRect(Rect rect)
        {
            Attribute.Rect = rect;
            // 移动
            Attribute.Position = new Point(rect.X, rect.Y);
            Render();
        }

        #region IEditableDrawingVisual 实现

        /// <summary>
        /// 是否支持双击编辑
        /// </summary>
        public bool SupportsDoubleClickEditing => true;

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        public bool IsEditing => _isEditing;

        /// <summary>
        /// 开始编辑
        /// </summary>
        public void BeginEdit(DrawCanvas canvas)
        {
            if (_isEditing) return;

            // 获取包含 DrawCanvas 的父元素
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(canvas) as UIElement;
            while (parent != null && !(parent is Canvas) && !(parent is Grid) && !(parent is Panel))
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent) as UIElement;
            }

            if (parent == null || !(parent is Panel panel))
            {
                // 如果没有合适的父容器，无法编辑
                return;
            }

            _isEditing = true;
            Attribute.IsEditing = true;

            // 创建 TextBox 用于编辑
            _editTextBox = new TextBox
            {
                Text = TextAttribute.Text,
                FontSize = TextAttribute.FontSize,
                FontFamily = TextAttribute.FontFamily,
                FontStyle = TextAttribute.FontStyle,
                FontWeight = TextAttribute.FontWeight,
                FontStretch = TextAttribute.FontStretch,
                Foreground = TextAttribute.Brush,
                Background = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Blue,
                Padding = new Thickness(2),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 50,
                MinHeight = TextAttribute.FontSize + 10
            };

            // 计算 TextBox 的位置和大小
            var rect = GetRect();
            _editTextBox.Width = Math.Max(rect.Width + 20, 100);
            _editTextBox.Height = Math.Max(rect.Height + 10, TextAttribute.FontSize + 10);

            // 获取 DrawCanvas 在父容器中的位置
            var position = canvas.TranslatePoint(Attribute.Position, parent);

            // 将 TextBox 添加到父容器
            panel.Children.Add(_editTextBox);

            // 设置位置
            if (_editTextBox.Parent is Canvas canvasParent)
            {
                Canvas.SetLeft(_editTextBox, position.X);
                Canvas.SetTop(_editTextBox, position.Y);
            }
            else
            {
                // 使用 Margin 定位
                _editTextBox.Margin = new Thickness(position.X, position.Y, 0, 0);
                _editTextBox.HorizontalAlignment = HorizontalAlignment.Left;
                _editTextBox.VerticalAlignment = VerticalAlignment.Top;
            }

            // 设置焦点并选中文本
            _editTextBox.Focus();
            _editTextBox.SelectAll();

            // 存储原始文本，用于取消编辑
            string originalText = TextAttribute.Text;

            // 处理文本变更
            _editTextBox.TextChanged += (s, e) =>
            {
                // 自动调整大小
                var formattedText = new FormattedText(
                    _editTextBox.Text,
                    CultureInfo.CurrentCulture,
                    TextAttribute.FlowDirection,
                    new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch),
                    TextAttribute.FontSize,
                    TextAttribute.Brush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                _editTextBox.Width = Math.Max(formattedText.Width + 30, 100);
                _editTextBox.Height = Math.Max(formattedText.Height + 15, TextAttribute.FontSize + 10);
            };

            // 处理按键事件
            _editTextBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    // Enter 键结束编辑（Shift+Enter 换行）
                    e.Handled = true;
                    EndEdit(canvas, panel, true);
                }
                else if (e.Key == Key.Escape)
                {
                    // Escape 取消编辑
                    e.Handled = true;
                    _editTextBox.Text = originalText; // 恢复原始文本
                    EndEdit(canvas, panel, true);
                }
            };

            // 处理失去焦点
            _editTextBox.LostFocus += (s, e) =>
            {
                // 延迟检查，避免点击其他位置时立即关闭
                if (_isEditing)
                {
                    EndEdit(canvas, panel, true);
                }
            };
        }

        /// <summary>
        /// 结束编辑
        /// </summary>
        public void EndEdit(DrawCanvas canvas, Panel parent, bool saveChanges)
        {
            if (!_isEditing) return;

            if (saveChanges && _editTextBox != null)
            {
                TextAttribute.Text = _editTextBox.Text;
            }

            // 移除 TextBox
            if (_editTextBox != null && parent.Children.Contains(_editTextBox))
            {
                parent.Children.Remove(_editTextBox);
                _editTextBox = null;
            }

            _isEditing = false;
            Attribute.IsEditing = false;

            // 重新渲染
            Render();
        }

        /// <summary>
        /// 处理双击事件
        /// </summary>
        public bool HandleDoubleClick(DrawCanvas canvas, Point point)
        {
            if (GetRect().Contains(point))
            {
                BeginEdit(canvas);
                return true;
            }
            return false;
        }

        #endregion
    }

    /// <summary>
    /// 可编辑的绘图视觉接口
    /// </summary>
    public interface IEditableDrawingVisual
    {
        /// <summary>
        /// 是否支持双击编辑
        /// </summary>
        bool SupportsDoubleClickEditing { get; }

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        bool IsEditing { get; }

        /// <summary>
        /// 开始编辑
        /// </summary>
        void BeginEdit(DrawCanvas canvas);

        /// <summary>
        /// 结束编辑
        /// </summary>
        /// <param name="canvas">画布</param>
        /// <param name="parent">父容器</param>
        /// <param name="saveChanges">是否保存更改</param>
        void EndEdit(DrawCanvas canvas, Panel parent, bool saveChanges);

        /// <summary>
        /// 处理双击事件
        /// </summary>
        /// <param name="canvas">画布</param>
        /// <param name="point">点击位置</param>
        /// <returns>是否处理了事件</returns>
        bool HandleDoubleClick(DrawCanvas canvas, Point point);
    }
}

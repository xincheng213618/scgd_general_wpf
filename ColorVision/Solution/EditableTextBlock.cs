using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution
{
    public class EditableTextBlock : UserControl
    {
        #region IsEditMode Property
        public static readonly DependencyProperty IsEditModeProperty =
            DependencyProperty.Register("IsEditMode", typeof(bool), typeof(EditableTextBlock), new PropertyMetadata(false));

        public bool IsEditMode
        {
            get { return (bool)GetValue(IsEditModeProperty); }
            set { SetValue(IsEditModeProperty, value); }
        }
        #endregion
    }
}

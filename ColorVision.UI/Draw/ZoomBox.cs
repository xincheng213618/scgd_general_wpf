using Gu.Wpf.Geometry;
using System.Windows.Input;
using System.Windows;
using System.ComponentModel;

namespace ColorVision.Engine.Draw
{
    public class ZoomboxSub : Zoombox, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ModifierKeys ActivateOn
        {
            get { return (ModifierKeys)GetValue(ActivateOnProperty); }
            set { SetValue(ActivateOnProperty, value); }
        }

        public static readonly DependencyProperty ActivateOnProperty = DependencyProperty.Register(nameof(ActivateOn), typeof(ModifierKeys), typeof(Zoombox), new PropertyMetadata(ModifierKeys.None));

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);
            if (ActivateOn == ModifierKeys.None || Keyboard.Modifiers.HasFlag(ActivateOn))
            {
                base.OnMouseLeftButtonDown(e);
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            if (ActivateOn == ModifierKeys.None || Keyboard.Modifiers.HasFlag(ActivateOn))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("OnMouseWheel"));
                base.OnMouseWheel(e);
            }
        }
        public new void ZoomUniformToFill()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ZoomUniformToFill"));
            base.ZoomUniformToFill();
        }

        public new void Zoom(double scale)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Zoom"));
            base.Zoom(scale);
        }

        public new void ZoomUniform()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ZoomUniform"));
            base.ZoomUniform();
        }
        public new void ZoomNone()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ZoomNone"));
            base.ZoomNone();
        }

    }
}

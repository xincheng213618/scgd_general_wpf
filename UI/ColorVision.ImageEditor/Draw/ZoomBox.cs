using Gu.Wpf.Geometry;
using System.Windows.Input;
using System.Windows;
using System;

namespace ColorVision.ImageEditor
{
    public class ZoomboxSub : Zoombox
    {

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
                base.OnMouseWheel(e);
            }
        }
        public new void ZoomUniformToFill()
        {
            base.ZoomUniformToFill();
        }

        public new void Zoom(double scale)
        {
            base.Zoom(scale);
        }

        public new void ZoomUniform()
        {
            base.ZoomUniform();
        }
        public new void ZoomNone()
        {
            base.ZoomNone();
        }

    }
}

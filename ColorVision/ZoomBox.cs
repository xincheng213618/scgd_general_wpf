using Gu.Wpf.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.ComponentModel;

namespace ColorVision
{
    public class ZoomboxSub:Zoombox, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ModifierKeys ActivateOn
        {
            get { return (ModifierKeys)GetValue(ActivateOnProperty); }
            set { SetValue(ActivateOnProperty, value); }
        }

        public static readonly DependencyProperty ActivateOnProperty = DependencyProperty.Register(nameof(ActivateOn), typeof(ModifierKeys), typeof(Zoombox), new PropertyMetadata(ModifierKeys.None));

        /// <inheritdoc />
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }
            if (ActivateOn == ModifierKeys.None || Keyboard.Modifiers.HasFlag(ActivateOn))
            {
                base.OnMouseLeftButtonDown(e);
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (e is null)
            {
                throw new ArgumentNullException(nameof(e));
            }
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

using Gu.Wpf.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace ColorVision
{
    public class ZoomboxSub:Zoombox
    {
        public ModifierKeys ActivateOn
        {
            get { return (ModifierKeys)GetValue(ActivateOnProperty); }
            set { SetValue(ActivateOnProperty, value); }
        }

        public static readonly DependencyProperty ActivateOnProperty = DependencyProperty.Register(nameof(ActivateOn), typeof(ModifierKeys), typeof(Zoombox), new PropertyMetadata(ModifierKeys.Control));

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



    }
}

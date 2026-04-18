using ColorVision.ImageEditor.Abstractions;
using System;
using System.Windows;

namespace ColorVision.ImageEditor
{
    public partial class ImageView
    {
        private PseudoColorController? _pseudoColorController;

        private void InitializePseudoColor()
        {
            ComColormapTypes.ItemsSource = ColormapConstats.PreviewResources;
            Config.ColormapTypesChanged += Config_ColormapTypesChanged;
            Config.AutoSetRangeChanged += Config_AutoSetRangeChanged;

            _pseudoColorController = new PseudoColorController(this);
            EditorContext.RegisterService<IPseudoColorService>(_pseudoColorController);
            _pseudoColorController.RefreshPreview();
        }

        private void DisposePseudoColor()
        {
            Config.ColormapTypesChanged -= Config_ColormapTypesChanged;
            Config.AutoSetRangeChanged -= Config_AutoSetRangeChanged;
            EditorContext.UnregisterService<IPseudoColorService>();
            _pseudoColorController = null;
        }

        private void ConfigurePseudoColorForImage()
        {
            PseudoColorService.ConfigureForImage();
        }

        private void InvalidatePseudoColorRender()
        {
            PseudoColorService.Invalidate();
        }

        private void Config_ColormapTypesChanged(object? sender, EventArgs e)
        {
            _pseudoColorController?.OnColormapTypesChanged();
        }

        private void AutoSetRange_Click(object sender, RoutedEventArgs e)
        {
            _pseudoColorController?.OnAutoSetRangeRequested();
        }

        private void Config_AutoSetRangeChanged(object? sender, EventArgs e)
        {
            _pseudoColorController?.OnAutoSetRangeChanged();
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _pseudoColorController?.OnPseudoToggleChanged();
        }

        private void PseudoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            _pseudoColorController?.OnSliderValueChanged();
        }
    }
}
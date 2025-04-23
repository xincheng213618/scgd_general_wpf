using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Text
{
    public class TextProperties : BaseProperties
    {
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        public string Text
        {
            get => TextAttribute.Text; set { TextAttribute.Text = value; }
        }
    }
    public class DVText : DrawingVisualBase<TextProperties>, IDrawingVisual
    {
        public DVText(ZoomboxSub zombox, DrawCanvas drawCanvas)
        {

        }

        public bool AutoAttributeChanged { get => _AutoAttributeChanged; set { _AutoAttributeChanged = value; } }
        private bool _AutoAttributeChanged;

        public Pen Pen { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        public override void Render()
        {

        }
    }
}

using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using System.Windows;

namespace PropertyEditor
{
    internal class PropertyEditorWindow
    {
        private SelfAdaptionInitDark selfAdaptionInitDark;

        public PropertyEditorWindow(SelfAdaptionInitDark selfAdaptionInitDark)
        {
            this.selfAdaptionInitDark = selfAdaptionInitDark;
        }

        public Window Owner { get; set; }
        public WindowStartupLocation WindowStartupLocation { get; set; }
    }
}
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.Validate
{

    public class ExportValidue : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string GuidId => "Comply";
        public int Order => 4;
        public Visibility Visibility => Visibility.Visible;
        public string Header => Properties.Resources.MenuValidue;

        public string InputGestureText { get; }

        public object Icon { get; }

        public RelayCommand Command => new(a =>
        {
        });
    }
}

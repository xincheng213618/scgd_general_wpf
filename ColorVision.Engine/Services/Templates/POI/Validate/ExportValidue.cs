using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Services.Templates.POI.Validate
{

    public class ExportValidue : IMenuItem
    {
        public string OwnerGuid => "Template";

        public string GuidId => "Validue";
        public int Order => 4;
        public Visibility Visibility => Visibility.Visible;
        public string Header => Engine.Properties.Resources.MenuValidue;

        public string InputGestureText { get; }

        public object Icon { get; }

        public RelayCommand Command => new(a =>
        {
        });
    }

    public class ExportValidueCIE : IMenuItem
    {
        public string OwnerGuid => "Validue";

        public string GuidId => "ValidueCIE";
        public int Order => 1;
        public Visibility Visibility => Visibility.Visible;
        public string Header => "CIE合规";

        public string InputGestureText { get; }

        public object Icon { get; }

        public RelayCommand Command => new(a =>
        {
            new WindowTemplate(new TemplateValidateParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class ExportValidueCIEAVG : IMenuItem
    {
        public string OwnerGuid => "Validue";

        public string GuidId => "ValidueCIEAVG";
        public int Order => 2;
        public Visibility Visibility => Visibility.Visible;
        public string Header => "CIE均值合规";

        public string InputGestureText { get; }

        public object Icon { get; }

        public RelayCommand Command => new RelayCommand(a =>
        {
            new WindowTemplate(new TemplateValidateCIEAVGParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }
}

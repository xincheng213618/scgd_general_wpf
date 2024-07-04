using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.Validate
{

    public class ExportValidueCIEAVG : IMenuItem
    {
        public string OwnerGuid => "Comply";

        public string GuidId => "ComplyCIEAVG";
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

    public class TemplateValidateCIEAVGParam : TemplateValidateParam
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> CIEAVGParams { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();

        public TemplateValidateCIEAVGParam()
        {
            Title = "Comply.CIE.AVG";
            TemplateParams = CIEAVGParams;
            Code = "Comply.CIE.AVG";
            IsUserControl = true;
            ValidateControl = new ValidateControl();
        }
    }
}

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.Comply
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
            new WindowTemplate(new TemplateComplyCIEAVGParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class TemplateComplyCIEAVGParam : TemplateComplyParam
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> CIEAVGParams { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();

        public TemplateComplyCIEAVGParam()
        {
            Title = "Comply.CIE.AVG";
            TemplateParams = CIEAVGParams;
            Code = "Comply.CIE.AVG";
            IsUserControl = true;
            ValidateControl = new ValidateControl();
        }
    }
}

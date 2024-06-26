using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.Validate
{
    public class ExportValidueCIESTDEV : IMenuItem
    {
        public string OwnerGuid => "Validue";

        public string GuidId => "ValidueCIESTDEV";
        public int Order => 2;
        public Visibility Visibility => Visibility.Visible;
        public string Header => "CIE标准差合规";

        public string InputGestureText { get; }

        public object Icon { get; }

        public RelayCommand Command => new RelayCommand(a =>
        {
            new WindowTemplate(new TemplateValidateCIESTDEVParam()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class TemplateValidateCIESTDEVParam : TemplateValidateParam
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> CIESTDEVParams { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();
        public TemplateValidateCIESTDEVParam()
        {
            Title = "Comply.CIE.STDEV";
            TemplateParams = CIESTDEVParams;
            Code = "Comply.CIE.STDEV";
            IsUserControl = true;
            ValidateControl = new ValidateControl();
        }
    }
}

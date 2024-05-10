using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Templates;
using ColorVision.UI;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Dao.Validate
{

    public class ExportValidue : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "Validue";
        public int Order => 2;
        public string? Header => ColorVision.Properties.Resource.MenuValidue;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new RelayCommand(a => {
            new WindowTemplate(TemplateType.ValidateParam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class ValidateParam : ParamBase
    {
        public static ObservableCollection<TemplateModel<ValidateParam>> Params { get; set; } = new ObservableCollection<TemplateModel<ValidateParam>>();



    }
}

using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Services.Templates;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Services.Dao.Validate
{

    public class BuildPOIMenuItem : IMenuItem
    {
        public string? OwnerGuid => "Template";

        public string? GuidId => "Validue";
        public int Index => 2;
        public string? Header => "校验模板设置(_B)";

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new RelayCommand(a => {
            new WindowTemplate(TemplateType.BuildPOIParmam) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        });
    }

    public class ValidateParam : ParamBase
    {



    }
}

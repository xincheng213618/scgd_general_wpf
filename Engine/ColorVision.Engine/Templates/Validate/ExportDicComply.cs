using ColorVision.Database;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.Templates.Validate
{
    public class ExportDicComply : MenuItemBase
    {
        public override string OwnerGuid => nameof(ExportComply);

        public override string GuidId => "ComplyEdit";
        public override int Order => 999;
        public override string Header =>ColorVision.Engine.Properties.Resources.EditDefaultComplianceDictionary;
        public override void Execute()
        {
            new TemplateEditorWindow(new TemplateDicComply()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }


}

using ColorVision.Engine.Templates.Menus;
using ColorVision.UI.Menus;
using ColorVision.Engine.Templates;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class MenuTemplateSensor : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 13;
        public override string Header => Properties.Resources.MenuSensor;

        public override void Execute()
        {
            new TemplateEditorWindow(new TemplateSensor("Sensor.Default")) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

}

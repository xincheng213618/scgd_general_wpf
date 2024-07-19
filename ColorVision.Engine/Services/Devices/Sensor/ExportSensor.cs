using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates.POI.Comply.Dao;
using ColorVision.Engine.Templates.POI.Comply;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using ColorVision.Themes;

namespace ColorVision.Engine.Services.Devices.Sensor
{
    public class ExportSensor : MenuItemBase
    {
        public override string OwnerGuid => "Template";

        public override string GuidId => "TemplateSensor";
        public override int Order => 21;
        public override string Header => Properties.Resources.MenuSensor;
    }

}

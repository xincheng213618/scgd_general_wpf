using ColorVision.Database;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{
    internal static class RCConfigurationActions
    {
        internal static void OpenServiceRegistryConfiguration()
        {
            SysConfigRcModel? config = SysConfigRcDao.Instance.GetByCode(RCSetting.Instance.Config.RCName);
            if (config == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.Engine_Msg_RCConfigInfoNotFound, RCSetting.Instance.Config.RCName), "ColorVision");
                return;
            }

            PropertyEditorWindow editor = new(config, PropertyEditorEditMode.Transactional)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            editor.Submitted += (s, e) => SysConfigRcDao.Instance.Save(config);
            editor.ShowDialog();
        }
    }
}

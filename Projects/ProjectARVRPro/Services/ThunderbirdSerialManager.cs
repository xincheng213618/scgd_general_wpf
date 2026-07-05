using ColorVision.Common.MVVM;
using System.Windows;

namespace ProjectARVRPro.Services
{
    public class ThunderbirdSerialManager
    {
        private static ThunderbirdSerialManager _instance;
        private static readonly object _locker = new();
        private ThunderbirdSerialDebugWindow? _window;

        public static ThunderbirdSerialManager GetInstance()
        {
            lock (_locker)
            {
                _instance ??= new ThunderbirdSerialManager();
                return _instance;
            }
        }

        public RelayCommand EditCommand { get; }

        private ThunderbirdSerialManager()
        {
            EditCommand = new RelayCommand(_ => Edit());
        }

        private void Edit()
        {
            if (_window == null)
            {
                _window = new ThunderbirdSerialDebugWindow
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                _window.Closed += (_, _) => _window = null;
                _window.Show();
                return;
            }

            _window.Activate();
        }
    }
}

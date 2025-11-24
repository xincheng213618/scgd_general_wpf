using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Examples
{
    /// <summary>
    /// Example demonstrating PropertyVisibilityAttribute with enum support
    /// This shows how to use the extended PropertyVisibilityAttribute to control
    /// property visibility based on enum values in addition to boolean values.
    /// </summary>
    [DisplayName("Enum Visibility Example")]
    public class EnumVisibilityExample : ViewModelBase
    {
        // Define an enum for operation mode
        public enum OperationMode
        {
            [Description("Basic mode with limited options")]
            Basic,
            
            [Description("Advanced mode with more options")]
            Advanced,
            
            [Description("Expert mode with all options")]
            Expert
        }

        // Define another enum for connection type
        public enum ConnectionType
        {
            USB,
            Ethernet,
            Serial
        }

        #region Mode Selection

        [Category("General")]
        [DisplayName("Operation Mode")]
        [Description("Select the operation mode")]
        public OperationMode Mode 
        { 
            get => _Mode; 
            set 
            { 
                _Mode = value; 
                OnPropertyChanged(); 
            } 
        }
        private OperationMode _Mode = OperationMode.Basic;

        [Category("General")]
        [DisplayName("Connection Type")]
        [Description("Select the connection type")]
        public ConnectionType Connection 
        { 
            get => _Connection; 
            set 
            { 
                _Connection = value; 
                OnPropertyChanged(); 
            } 
        }
        private ConnectionType _Connection = ConnectionType.USB;

        #endregion

        #region Basic Mode Settings (visible only in Basic mode)

        [Category("Basic Settings")]
        [DisplayName("Basic Parameter 1")]
        [Description("This parameter is only visible in Basic mode")]
        [PropertyVisibility(nameof(Mode), OperationMode.Basic)]
        public string BasicParam1 
        { 
            get => _BasicParam1; 
            set 
            { 
                _BasicParam1 = value; 
                OnPropertyChanged(); 
            } 
        }
        private string _BasicParam1 = "Basic Value";

        [Category("Basic Settings")]
        [DisplayName("Basic Parameter 2")]
        [Description("Another parameter only visible in Basic mode")]
        [PropertyVisibility(nameof(Mode), OperationMode.Basic)]
        public int BasicParam2 
        { 
            get => _BasicParam2; 
            set 
            { 
                _BasicParam2 = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _BasicParam2 = 10;

        #endregion

        #region Advanced Mode Settings (visible only in Advanced mode)

        [Category("Advanced Settings")]
        [DisplayName("Advanced Parameter 1")]
        [Description("This parameter is only visible in Advanced mode")]
        [PropertyVisibility(nameof(Mode), OperationMode.Advanced)]
        public double AdvancedParam1 
        { 
            get => _AdvancedParam1; 
            set 
            { 
                _AdvancedParam1 = value; 
                OnPropertyChanged(); 
            } 
        }
        private double _AdvancedParam1 = 3.14;

        [Category("Advanced Settings")]
        [DisplayName("Advanced Parameter 2")]
        [Description("Another parameter only visible in Advanced mode")]
        [PropertyVisibility(nameof(Mode), OperationMode.Advanced)]
        public bool AdvancedParam2 
        { 
            get => _AdvancedParam2; 
            set 
            { 
                _AdvancedParam2 = value; 
                OnPropertyChanged(); 
            } 
        }
        private bool _AdvancedParam2 = true;

        #endregion

        #region Expert Mode Settings (visible only in Expert mode)

        [Category("Expert Settings")]
        [DisplayName("Expert Parameter 1")]
        [Description("This parameter is only visible in Expert mode")]
        [PropertyVisibility(nameof(Mode), OperationMode.Expert)]
        public string ExpertParam1 
        { 
            get => _ExpertParam1; 
            set 
            { 
                _ExpertParam1 = value; 
                OnPropertyChanged(); 
            } 
        }
        private string _ExpertParam1 = "Expert Value";

        #endregion

        #region Non-Basic Settings (visible in Advanced and Expert modes - using inverted logic)

        [Category("Non-Basic Settings")]
        [DisplayName("Non-Basic Parameter")]
        [Description("This parameter is hidden in Basic mode (visible in Advanced and Expert)")]
        [PropertyVisibility(nameof(Mode), OperationMode.Basic, isInverted: true)]
        public int NonBasicParam 
        { 
            get => _NonBasicParam; 
            set 
            { 
                _NonBasicParam = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _NonBasicParam = 100;

        #endregion

        #region Connection-specific Settings

        // USB Connection Settings
        [Category("Connection Settings")]
        [DisplayName("USB Port")]
        [Description("USB port identifier (visible only when Connection is USB)")]
        [PropertyVisibility(nameof(Connection), ConnectionType.USB)]
        public string UsbPort 
        { 
            get => _UsbPort; 
            set 
            { 
                _UsbPort = value; 
                OnPropertyChanged(); 
            } 
        }
        private string _UsbPort = "COM1";

        [Category("Connection Settings")]
        [DisplayName("USB Speed")]
        [Description("USB transfer speed (visible only when Connection is USB)")]
        [PropertyVisibility(nameof(Connection), ConnectionType.USB)]
        public int UsbSpeed 
        { 
            get => _UsbSpeed; 
            set 
            { 
                _UsbSpeed = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _UsbSpeed = 480;

        // Ethernet Connection Settings
        [Category("Connection Settings")]
        [DisplayName("IP Address")]
        [Description("Network IP address (visible only when Connection is Ethernet)")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Ethernet)]
        public string IpAddress 
        { 
            get => _IpAddress; 
            set 
            { 
                _IpAddress = value; 
                OnPropertyChanged(); 
            } 
        }
        private string _IpAddress = "192.168.1.100";

        [Category("Connection Settings")]
        [DisplayName("Port")]
        [Description("Network port (visible only when Connection is Ethernet)")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Ethernet)]
        public int Port 
        { 
            get => _Port; 
            set 
            { 
                _Port = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _Port = 8080;

        // Serial Connection Settings
        [Category("Connection Settings")]
        [DisplayName("Baud Rate")]
        [Description("Serial baud rate (visible only when Connection is Serial)")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Serial)]
        public int BaudRate 
        { 
            get => _BaudRate; 
            set 
            { 
                _BaudRate = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _BaudRate = 9600;

        [Category("Connection Settings")]
        [DisplayName("Data Bits")]
        [Description("Serial data bits (visible only when Connection is Serial)")]
        [PropertyVisibility(nameof(Connection), ConnectionType.Serial)]
        public int DataBits 
        { 
            get => _DataBits; 
            set 
            { 
                _DataBits = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _DataBits = 8;

        #endregion

        #region Boolean-based Visibility (Original Feature - Still Works)

        [Category("General")]
        [DisplayName("Enable Debugging")]
        [Description("Enable debugging features")]
        public bool EnableDebugging 
        { 
            get => _EnableDebugging; 
            set 
            { 
                _EnableDebugging = value; 
                OnPropertyChanged(); 
            } 
        }
        private bool _EnableDebugging = false;

        [Category("Debug Settings")]
        [DisplayName("Debug Level")]
        [Description("Debugging level (visible only when EnableDebugging is true)")]
        [PropertyVisibility(nameof(EnableDebugging))]
        public int DebugLevel 
        { 
            get => _DebugLevel; 
            set 
            { 
                _DebugLevel = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _DebugLevel = 1;

        [Category("Debug Settings")]
        [DisplayName("Log File Path")]
        [Description("Path to debug log file (visible only when EnableDebugging is true)")]
        [PropertyVisibility(nameof(EnableDebugging))]
        public string LogFilePath 
        { 
            get => _LogFilePath; 
            set 
            { 
                _LogFilePath = value; 
                OnPropertyChanged(); 
            } 
        }
        private string _LogFilePath = "debug.log";

        #endregion

        #region Common Settings (always visible)

        [Category("General")]
        [DisplayName("Device Name")]
        [Description("Name of the device")]
        public string DeviceName 
        { 
            get => _DeviceName; 
            set 
            { 
                _DeviceName = value; 
                OnPropertyChanged(); 
            } 
        }
        private string _DeviceName = "Example Device";

        [Category("General")]
        [DisplayName("Timeout (seconds)")]
        [Description("Operation timeout in seconds")]
        public int Timeout 
        { 
            get => _Timeout; 
            set 
            { 
                _Timeout = value; 
                OnPropertyChanged(); 
            } 
        }
        private int _Timeout = 30;

        #endregion
    }
}

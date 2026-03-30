using ColorVision.Themes;
using System.IO;
using System.Text;
using System.Windows;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// WindowLogLocal - standalone window for displaying external log files.
    /// Delegates all log display logic to the embedded LogLocalOutput UserControl.
    /// </summary>
    public partial class WindowLogLocal : Window
    {
        /// <summary>
        /// The log file path being monitored.
        /// </summary>
        public string LogFilePath { get; private set; }

        /// <summary>
        /// File encoding.
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.Default;

        /// <summary>
        /// The embedded LogLocalOutput control (exposed for external access if needed).
        /// </summary>
        public LogLocalOutput? LogLocalOutput { get; private set; }

        /// <summary>
        /// Create a new WindowLogLocal instance.
        /// </summary>
        /// <param name="logFilePath">Log file path</param>
        /// <param name="encoding">Optional encoding override</param>
        public WindowLogLocal(string logFilePath, Encoding? encoding = null)
        {
            LogFilePath = logFilePath;
            if (encoding != null)
            {
                Encoding = encoding;
            }
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Title = $"Log - {Path.GetFileName(LogFilePath)}";

            // Create and host the LogLocalOutput UserControl
            LogLocalOutput = new LogLocalOutput(LogFilePath, Encoding);
            RootGrid.Children.Add(LogLocalOutput);
        }
    }
}

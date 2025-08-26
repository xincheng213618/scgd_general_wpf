using ColorVision.UI;
using System;
using System.Linq;
using System.Windows;

namespace ColorVision.Plugins
{
    /// <summary>
    /// ViewDllVersionsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ViewDllVersionsWindow : Window
    {
        public ViewDllVersionsWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            var assemblies = AssemblyService.Instance.GetAssemblies();
            var dllInfos = assemblies
                .Select(a =>
                {
                    var name = a.GetName();
                    string fileVersion = "";
                    string productVersion = "";
                    string company = "";
                    string product = "";
                    try
                    {
                        if (!a.IsDynamic && System.IO.File.Exists(a.Location))
                        {
                            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(a.Location);
                            fileVersion = fvi.FileVersion;
                            productVersion = fvi.ProductVersion;
                            company = fvi.CompanyName;
                            product = fvi.ProductName;
                        }
                    }
                    catch { }
                    return new
                    {
                        Name = name.Name,
                        Version = name.Version?.ToString(),
                        FileVersion = fileVersion,
                        ProductVersion = productVersion,
                        Company = company,
                        Product = product,
                        Culture = name.CultureInfo?.Name,
                        PublicKeyToken = BitConverter.ToString(name.GetPublicKeyToken() ?? new byte[0]),
                        Location = a.IsDynamic ? "(dynamic)" : a.Location,
                        IsDynamic = a.IsDynamic,
                    };
                })
                .OrderBy(a => a.Name)
                .ToList();

            DllDataGrid.ItemsSource = dllInfos;
        }
    }
}

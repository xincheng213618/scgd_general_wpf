using ColorVision.Themes;
using ProjectStarkSemi.Conoscope;
using ProjectStarkSemi.Layout;
using System;
using System.Reflection;
using System.Windows;

namespace ProjectStarkSemi
{
    public class ConoscopeWindow : Window, IDisposable
    {
        public static ConoscopeWindow? Instance { get; private set; }

        public ConoscopeView View { get; }

        internal DockLayoutManager? LayoutManager => View.LayoutManager;

        public ConoscopeWindow()
        {
            Instance = this;
            Width = 1582;
            Height = 750;
            Title = "Conoscope " + (Assembly.GetAssembly(typeof(ConoscopeWindow))?.GetName().Version?.ToString() ?? string.Empty);
            SetResourceReference(BackgroundProperty, "GlobalBackground");
            Content = View = new ConoscopeView();
            this.ApplyCaption();
            ConoscopeWindowConfig.Instance.SetWindow(this);
            Closing += (s, e) => LayoutManager?.SaveLayout();
            Closed += (s, e) =>
            {
                if (ReferenceEquals(Instance, this))
                {
                    Instance = null;
                }

                Dispose();
            };
        }

        public void OpenConoscope(string filename)
        {
            View.OpenConoscope(filename);
            Title = $"Conoscope - {System.IO.Path.GetFileName(filename)}";
        }

        internal void RefreshConoscopeConfiguration()
        {
            View.RefreshConoscopeConfiguration();
        }

        public void Dispose()
        {
            View.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

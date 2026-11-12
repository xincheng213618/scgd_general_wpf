#pragma warning disable CA1720,CA1822,CA1854,CS8619
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Menus
{
    public partial class MenuManager
    {
        private sealed class MenuRegistration
        {
            private readonly MenuManager _owner;
            private readonly RoutedEventHandler _unloadedHandler;
            private RoutedEventHandler? _loadedHandler;
            private EventHandler? _closedHandler;
            private Window? _window;
            private bool _detached;

            public MenuRegistration(MenuManager owner, string targetName, Menu menu, Func<Type, bool>? typeFilter)
            {
                _owner = owner;
                TargetName = targetName;
                Menu = menu;
                TypeFilter = typeFilter;

                _unloadedHandler = OnMenuUnloaded;
                Menu.Unloaded += _unloadedHandler;
                AttachWindowIfAvailable();
                if (_window == null)
                {
                    _loadedHandler = OnMenuLoaded;
                    Menu.Loaded += _loadedHandler;
                }
            }

            public string TargetName { get; private set; }
            public Menu Menu { get; }
            public Func<Type, bool>? TypeFilter { get; private set; }

            public void Update(string targetName, Func<Type, bool>? typeFilter)
            {
                TargetName = targetName;
                TypeFilter = typeFilter;
                AttachWindowIfAvailable();
            }

            public void Detach()
            {
                if (_detached) return;

                _detached = true;
                Menu.Unloaded -= _unloadedHandler;
                if (_loadedHandler != null)
                {
                    Menu.Loaded -= _loadedHandler;
                    _loadedHandler = null;
                }

                if (_window != null && _closedHandler != null)
                {
                    _window.Closed -= _closedHandler;
                }

                _closedHandler = null;
                _window = null;
            }

            private void AttachWindowIfAvailable()
            {
                if (_window != null) return;

                var window = Window.GetWindow(Menu);
                if (window == null) return;

                _window = window;
                _closedHandler = OnWindowClosed;
                _window.Closed += _closedHandler;

                if (_loadedHandler != null)
                {
                    Menu.Loaded -= _loadedHandler;
                    _loadedHandler = null;
                }
            }

            private void OnMenuLoaded(object sender, RoutedEventArgs e)
            {
                AttachWindowIfAvailable();
            }

            private void OnMenuUnloaded(object sender, RoutedEventArgs e)
            {
                _owner.UnregisterMenu(Menu);
            }

            private void OnWindowClosed(object? sender, EventArgs e)
            {
                _owner.UnregisterMenu(Menu);
            }
        }
    }
}

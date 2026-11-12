#pragma warning disable CA1720,CA1822,CA1854,CS8619
using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Menus
{
    public partial class MenuManager
    {
        /// <summary>
        /// Adapts a <see cref="MenuItemAttribute"/>-decorated class to <see cref="IMenuItem"/>.
        /// All display metadata comes from the attribute; the underlying class is instantiated
        /// only when the menu item's command is executed (lazy loading).
        /// </summary>
        private sealed class LazyMenuItemAdapter : IMenuItem
        {
            private readonly MenuItemAttribute _attr;
            private readonly Type _type;
            private readonly MethodInfo? _executeMethod;
            private readonly RequiresPermissionAttribute? _permission;
            private readonly string _actionId;
            private ICommand? _lazyCommand;

            internal LazyMenuItemAdapter(MenuItemAttribute attr, Type type)
            {
                _attr = attr;
                _type = type;
                _executeMethod = type.GetMethod("Execute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                _permission = _executeMethod?.GetCustomAttribute<RequiresPermissionAttribute>(true)
                    ?? type.GetCustomAttribute<RequiresPermissionAttribute>(true);
                _actionId = !string.IsNullOrWhiteSpace(_permission?.ActionId) ? _permission.ActionId
                    : $"{_executeMethod?.DeclaringType?.FullName ?? type.FullName}.Execute";
            }

            public string TargetName => _attr.TargetName;
            public string? OwnerGuid => _attr.OwnerGuid;
            public string? GuidId => _attr.GuidId ?? _type.FullName;
            public int Order => _attr.Order;
            public string? Header => _attr.Header;
            public string? InputGestureText => _attr.InputGestureText;
            public object? Icon => null;
            public Visibility Visibility => Visibility.Visible;
            public bool? IsChecked => null;

            public ICommand? Command => _lazyCommand ??= new RelayCommand(_ => ExecuteUnderlying(), _ => IsAuthorized());

            private bool IsAuthorized() => AccessControl.Evaluate(_actionId, _permission?.RequiredPermission, _permission?.ResourceId).Allowed;

            private void ExecuteUnderlying()
            {
                if (!IsAuthorized()) return;
                try
                {
                    var instance = Activator.CreateInstance(_type);
                    if (instance is IMenuItem mi)
                    {
                        ICommand? command = mi.Command;
                        if (command?.CanExecute(null) == true)
                        {
                            AccessControl.Demand(_actionId, _permission?.RequiredPermission, _permission?.ResourceId);
                            command.Execute(null);
                        }
                    }
                    else
                    {
                        if (_executeMethod != null)
                        {
                            AccessControl.Demand(_actionId, _permission?.RequiredPermission, _permission?.ResourceId);
                            _executeMethod.Invoke(instance, null);
                        }
                        else
                            log.Warn($"[MenuItem] class {_type.FullName} has no parameterless Execute() method and does not implement IMenuItem.");
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"Execute [MenuItem] class failed: {_type.FullName}: {ex.Message}");
                }
            }
        }
    }
}

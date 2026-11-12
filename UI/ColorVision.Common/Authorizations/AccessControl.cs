using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows;

namespace ColorVision.UI.Authorizations
{
    public class Authorization : IConfig
    {
        public static Authorization Instance { get; set; } 

        public PermissionMode PermissionMode { get => _PermissionMode; set { _PermissionMode = value; OnPermissionModeChanged();  } }
        private PermissionMode _PermissionMode = PermissionMode.Administrator;

        [Browsable(false), IgnoreDataMember, System.Text.Json.Serialization.JsonIgnore]
        public string? SubjectId { get; set; }

        public event EventHandler PermissionModeChanged;

        protected virtual void OnPermissionModeChanged()
        {
            PermissionModeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static class AccessControl
    {
        private static IAccessPolicy policy = new RoleAccessPolicy();

        public static IAccessPolicy Policy
        {
            get => Volatile.Read(ref policy);
            set { ArgumentNullException.ThrowIfNull(value); Volatile.Write(ref policy, value); }
        }

        public static AccessSubject CurrentSubject
        {
            get
            {
                Authorization? authorization = Authorization.Instance;
                return authorization == null ? new(null, null) : new(authorization.SubjectId, authorization.PermissionMode);
            }
        }

        public static AccessDecision Evaluate(string actionId, PermissionMode? requiredPermission, string? resourceId = null, AccessSubject? subject = null)
        {
            AccessRequest request = new(actionId, requiredPermission, subject ?? CurrentSubject, resourceId);
            return EvaluateRequest(request);
        }

        public static void Demand(string actionId, PermissionMode? requiredPermission, string? resourceId = null, AccessSubject? subject = null)
        {
            AccessRequest request = new(actionId, requiredPermission, subject ?? CurrentSubject, resourceId);
            AccessDecision decision = EvaluateRequest(request);
            if (!decision.Allowed) throw new AccessDeniedException(request, decision);
        }

        private static AccessDecision EvaluateRequest(AccessRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.ActionId)) return new(false, "invalid_action");
            try
            {
                AccessDecision? decision = Policy.Evaluate(request);
                return decision != null && !string.IsNullOrWhiteSpace(decision.ReasonCode)
                    ? decision : new(false, "policy_unavailable");
            }
            catch (Exception)
            {
                return new(false, "policy_unavailable");
            }
        }

        public static bool Check(Action action)
        {
            var requirement = DescribeAction(action);
            return Evaluate(requirement.ActionId, requirement.Permission, requirement.ResourceId).Allowed;
        }

        public static bool Check(PermissionMode permissionMode) => Evaluate("legacy.permission.check", permissionMode).Allowed;

        public static void ExecuteWithPermissionCheck(Action action, PermissionMode currentPermission)
        {
            var requirement = DescribeAction(action);
            AccessDecision decision = Evaluate(requirement.ActionId, requirement.Permission, requirement.ResourceId, new AccessSubject(null, currentPermission));
            if (decision.Allowed) action();
            else MessageBox.Show("You do not have the required permission to execute this action.");
        }

        private static (string ActionId, PermissionMode? Permission, string? ResourceId) DescribeAction(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            var attribute = action.Method.GetCustomAttribute<RequiresPermissionAttribute>(true)
                ?? action.Target?.GetType().GetCustomAttribute<RequiresPermissionAttribute>(true)
                ?? action.Method.DeclaringType?.GetCustomAttribute<RequiresPermissionAttribute>(true);
            string actionId = !string.IsNullOrWhiteSpace(attribute?.ActionId) ? attribute.ActionId
                : $"{action.Method.DeclaringType?.FullName}.{action.Method.Name}";
            return (actionId, attribute?.RequiredPermission, attribute?.ResourceId);
        }
    }


}


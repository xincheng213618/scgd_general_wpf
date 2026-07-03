#pragma warning disable CS8604
using ColorVision.UI.ServiceHost;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.SocketProtocol
{
    internal sealed record FirewallStatus(string Summary, string Detail, bool CanAllow);

    internal sealed record FirewallProfileStatuses(FirewallStatus PrivateStatus, FirewallStatus PublicStatus);

    internal sealed record FirewallCommandResult(bool Success, string Message);

    internal static class SocketFirewallService
    {
        public static FirewallProfileStatuses GetStatuses(string? executablePath) =>
            WindowsFirewallStatusReader.GetProfileStatuses(executablePath);

        public static async Task<FirewallCommandResult> AllowApplicationAsync(string executablePath, string profile)
        {
            try
            {
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .AllowFirewallApplicationAsync(executablePath, profile, TimeSpan.FromSeconds(15))
                    .ConfigureAwait(true);
                string message = response.Success
                    ? $"{response.Message}\n\n已通过 ColorVisionServiceHost 完成。"
                    : $"ColorVisionServiceHost 执行失败：{response.Message}";
                return new FirewallCommandResult(response.Success, message);
            }
            catch (Exception ex)
            {
                return new FirewallCommandResult(false, $"ColorVisionServiceHost 不可用或版本过旧：{ex.Message}");
            }
        }
    }

    internal static class WindowsFirewallStatusReader
    {
        private const int NetFwRuleDirIn = 1;
        private const int NetFwActionBlock = 0;
        private const int NetFwActionAllow = 1;
        private const int NetFwProfileDomain = 1;
        private const int NetFwProfilePrivate = 2;
        private const int NetFwProfilePublic = 4;
        private const int NetFwProfileAll = int.MaxValue;

        public static FirewallProfileStatuses GetProfileStatuses(string? executablePath)
        {
            if (!OperatingSystem.IsWindows())
            {
                var status = new FirewallStatus("仅支持 Windows 检测", string.Empty, false);
                return new FirewallProfileStatuses(status, status);
            }

            object? policy = null;
            try
            {
                Type? policyType = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
                if (policyType == null)
                {
                    var status = new FirewallStatus("无法读取", "系统没有提供 HNetCfg.FwPolicy2 接口。", false);
                    return new FirewallProfileStatuses(status, status);
                }

                policy = Activator.CreateInstance(policyType);
                if (policy == null)
                {
                    var status = new FirewallStatus("无法读取", "Windows 防火墙策略对象创建失败。", false);
                    return new FirewallProfileStatuses(status, status);
                }

                dynamic firewallPolicy = policy;
                List<FirewallRuleMatch> matchedRules = FindMatchingRules(firewallPolicy.Rules, executablePath);
                FirewallStatus privateStatus = BuildProfileStatus(FirewallProfile.Private, matchedRules);
                FirewallStatus publicStatus = BuildProfileStatus(FirewallProfile.Public, matchedRules);
                return new FirewallProfileStatuses(privateStatus, publicStatus);
            }
            catch (Exception ex)
            {
                var status = new FirewallStatus("无法读取", ex.Message, false);
                return new FirewallProfileStatuses(status, status);
            }
            finally
            {
                if (policy != null && Marshal.IsComObject(policy))
                    Marshal.ReleaseComObject(policy);
            }
        }

        private static FirewallStatus BuildProfileStatus(FirewallProfile profile, List<FirewallRuleMatch> allRules)
        {
            List<FirewallRuleMatch> matchedRules = allRules.Where(rule => MatchesActiveProfile(rule.Profiles, profile.ProfileMask)).ToList();
            List<FirewallRuleMatch> blockRules = matchedRules.Where(rule => rule.Action == NetFwActionBlock).ToList();
            List<FirewallRuleMatch> allowRules = matchedRules.Where(rule => rule.Action == NetFwActionAllow).ToList();

            if (blockRules.Count > 0)
            {
                string detail = BuildDetail(profile.DisplayName, blockRules, allowRules);
                string summary = $"可能被阻止（{blockRules.Count} 条阻止规则）";
                return new FirewallStatus(summary, detail, true);
            }

            if (allowRules.Count > 0)
            {
                string detail = BuildDetail(profile.DisplayName, allowRules, blockRules);
                return new FirewallStatus($"已允许（{allowRules.Count} 条规则）", detail, false);
            }

            return new FirewallStatus("未放行", $"{profile.DisplayName}网络未找到当前程序的入站应用允许规则。", true);
        }

        private static List<FirewallRuleMatch> FindMatchingRules(dynamic rules, string? executablePath)
        {
            var matchedRules = new List<FirewallRuleMatch>();
            foreach (dynamic rule in rules)
            {
                FirewallRuleMatch? match = TryMatchRule(rule, executablePath);
                if (match != null)
                    matchedRules.Add(match);
            }
            return matchedRules;
        }

        private static FirewallRuleMatch? TryMatchRule(dynamic rule, string? executablePath)
        {
            try
            {
                if (!SafeGetBool(() => rule.Enabled))
                    return null;

                int direction = SafeGetInt(() => rule.Direction);
                if (direction != NetFwRuleDirIn)
                    return null;

                int profiles = SafeGetInt(() => rule.Profiles);
                string applicationName = SafeGetString(() => rule.ApplicationName);
                if (!MatchesApplication(applicationName, executablePath))
                    return null;

                return new FirewallRuleMatch
                {
                    Name = SafeGetString(() => rule.Name),
                    Action = SafeGetInt(() => rule.Action),
                    Profiles = profiles,
                    ApplicationName = applicationName,
                    RemoteAddresses = SafeGetString(() => rule.RemoteAddresses)
                };
            }
            catch
            {
                return null;
            }
        }

        private static bool MatchesActiveProfile(int ruleProfiles, int activeProfiles)
        {
            return activeProfiles == 0
                || ruleProfiles == 0
                || ruleProfiles == NetFwProfileAll
                || (ruleProfiles & activeProfiles) != 0;
        }

        private static bool MatchesApplication(string applicationName, string? executablePath)
        {
            return !string.IsNullOrWhiteSpace(applicationName)
                && !string.IsNullOrWhiteSpace(executablePath)
                && string.Equals(Environment.ExpandEnvironmentVariables(applicationName), executablePath, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDetail(string profileText, List<FirewallRuleMatch> primaryRules, List<FirewallRuleMatch> secondaryRules)
        {
            IEnumerable<string> primaryLines = primaryRules.Take(3).Select(FormatRule);
            string detail = $"{profileText}网络匹配规则：{string.Join("；", primaryLines)}";
            if (primaryRules.Count > 3)
                detail += $"；另有 {primaryRules.Count - 3} 条匹配规则";
            if (secondaryRules.Count > 0)
                detail += $"。同时还命中 {secondaryRules.Count} 条相反动作规则，请以 Windows 防火墙高级设置为准。";
            return detail;
        }

        private static string FormatRule(FirewallRuleMatch rule)
        {
            string scope = string.IsNullOrWhiteSpace(rule.RemoteAddresses) || rule.RemoteAddresses == "*"
                ? "任意远程地址"
                : $"远程地址 {rule.RemoteAddresses}";
            string target = Path.GetFileName(Environment.ExpandEnvironmentVariables(rule.ApplicationName));
            return $"{rule.Name}（{FormatProfileMask(rule.Profiles)}，{target}，{scope}）";
        }

        private static string FormatProfileMask(int profileMask)
        {
            if (profileMask == 0 || profileMask == NetFwProfileAll)
                return "所有网络";

            var names = new List<string>();
            if ((profileMask & NetFwProfileDomain) != 0)
                names.Add("域");
            if ((profileMask & NetFwProfilePrivate) != 0)
                names.Add("专用");
            if ((profileMask & NetFwProfilePublic) != 0)
                names.Add("公用");
            return names.Count == 0 ? profileMask.ToString(CultureInfo.InvariantCulture) : string.Join("/", names);
        }

        private static int SafeGetInt(Func<dynamic> getter)
        {
            try
            {
                object? value = getter();
                return value == null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static bool SafeGetBool(Func<dynamic> getter)
        {
            try
            {
                object? value = getter();
                return value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return false;
            }
        }

        private static string SafeGetString(Func<dynamic> getter)
        {
            try
            {
                return getter()?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private sealed class FirewallRuleMatch
        {
            public string Name { get; init; } = string.Empty;
            public int Action { get; init; }
            public int Profiles { get; init; }
            public string ApplicationName { get; init; } = string.Empty;
            public string RemoteAddresses { get; init; } = string.Empty;
        }
    }

    internal sealed record FirewallProfile(string DisplayName, int ProfileMask)
    {
        public static FirewallProfile Private { get; } = new("专用", 2);
        public static FirewallProfile Public { get; } = new("公用", 4);
    }
}

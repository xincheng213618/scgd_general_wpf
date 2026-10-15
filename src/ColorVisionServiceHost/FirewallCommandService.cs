using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace ColorVisionServiceHost;

internal static class FirewallCommandService
{
    private const int KnownProfiles = 1 | 2 | 4;
    private const int AllProfiles = int.MaxValue;
    private static readonly object PolicyGate = new();

    public static ServiceHostResponse AllowApplication(ServiceHostRequest request)
    {
        string? requestedPath = request.Data?["appPath"]?.ToString();
        if (string.IsNullOrWhiteSpace(requestedPath))
            throw new InvalidOperationException("Missing request data: appPath");

        string appPath = Path.GetFullPath(requestedPath);
        if (!File.Exists(appPath))
            return ServiceHostResponse.FromObject(request.RequestId, false, $"Application executable was not found: {appPath}");

        string profile = request.Data?["profile"]?.ToString() ?? string.Empty;
        FirewallAllowResult result;
        lock (PolicyGate)
        {
            result = AllowApplicationCore(appPath, profile);
        }
        return ServiceHostResponse.FromObject(request.RequestId, result.Success, result.Message, new
        {
            appPath,
            profile = result.Profile,
            ruleName = result.RuleName,
            resolvedBlockRules = result.ResolvedBlockRules,
            identity = WindowsIdentity.GetCurrent().Name,
            isElevated = IsElevated(),
        });
    }

    private static FirewallAllowResult AllowApplicationCore(string appPath, string profile)
    {
        object? policy = null;
        object? rules = null;
        var retainedRules = new List<object>();
        try
        {
            policy = CreateComObject("HNetCfg.FwPolicy2");
            dynamic firewallPolicy = policy;
            int profileMask = ResolveProfileMask(profile, (int)firewallPolicy.CurrentProfileTypes);
            // Application exceptions cannot override a profile's global inbound block.
            foreach (int selectedProfile in new[] { 1, 2, 4 })
            {
                if ((profileMask & selectedProfile) != 0 && (bool)firewallPolicy.BlockAllInboundTraffic[selectedProfile])
                    throw new InvalidOperationException($"{FormatProfileDisplay(selectedProfile)}网络设置了阻止所有入站连接，请在 Windows 防火墙高级设置中检查策略。");
            }

            rules = firewallPolicy.Rules;
            return ConfigureApplicationRules(appPath, profileMask, () =>
            {
                var snapshot = new List<object>();
                foreach (object rule in (dynamic)rules)
                {
                    retainedRules.Add(rule);
                    snapshot.Add(rule);
                }
                return snapshot;
            }, (name, path, profiles) =>
            {
                object rule = CreateComObject("HNetCfg.FWRule");
                retainedRules.Add(rule);
                dynamic allowRule = rule;
                allowRule.Name = name;
                allowRule.Description = "ColorVision application inbound access";
                allowRule.ApplicationName = path;
                allowRule.Protocol = 256;
                allowRule.Direction = 1;
                allowRule.Action = 1;
                allowRule.Profiles = profiles;
                allowRule.InterfaceTypes = "All";
                allowRule.Enabled = true;
                ((dynamic)rules).Add(allowRule);
            });
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Firewall application access failed. App={appPath}, Profile={profile}: {ex}");
            return new(false, string.Empty, profile, 0, $"防火墙放行失败：{ex.Message}");
        }
        finally
        {
            foreach (object rule in retainedRules)
                ReleaseComObject(rule);
            ReleaseComObject(rules);
            ReleaseComObject(policy);
        }
    }

    // Exercise these operations against in-memory rules in tests, without changing
    // the maintenance computer's firewall or requiring administrator access.
    internal static FirewallAllowResult ConfigureApplicationRules(string appPath, int profileMask,
        Func<IReadOnlyList<object>> readRules, Action<string, string, int> addAllowRule)
    {
        if (profileMask == 0 || (profileMask & ~KnownProfiles) != 0)
            throw new ArgumentOutOfRangeException(nameof(profileMask));

        string ruleName = BuildFirewallRuleName(appPath, profileMask);
        string profile = FormatProfileArgument(profileMask);
        int resolved = 0;
        try
        {
            IReadOnlyList<object> before = readRules();
            // Never delete by display name: another installation can have the same
            // executable name. Our rule name includes a digest of the full path.
            var ownedRules = before.Where(rule => (string)((dynamic)rule).Name == ruleName).ToList();
            if (ownedRules.Any(rule => !IsApplicationAllow(rule, appPath, profileMask, requireEnabled: false)))
                throw new InvalidOperationException($"同名规则与预期范围不一致，请检查规则：{ruleName}");
            if (ownedRules.Count == 0)
                addAllowRule(ruleName, appPath, profileMask);
            else
                foreach (dynamic rule in ownedRules)
                    rule.Enabled = true;

            foreach (dynamic rule in before.Where(rule => IsApplicationBlock(rule, appPath, profileMask)))
            {
                int originalProfiles = rule.Profiles;
                int remainingProfiles = NormalizeProfiles(originalProfiles) & ~profileMask;
                // Retain blocking on unselected networks. Disable a selected-only
                // rule instead of deleting it so the change remains inspectable.
                if (remainingProfiles == 0)
                    rule.Enabled = false;
                else
                    rule.Profiles = remainingProfiles;
                resolved++;
                ServiceHostLog.Write($"Firewall block scope updated. Rule={rule.Name}, App={appPath}, Profiles={originalProfiles}, RemainingProfiles={remainingProfiles}");
            }

            IReadOnlyList<object> after = readRules();
            if (after.Any(rule => IsApplicationBlock(rule, appPath, profileMask)))
                throw new InvalidOperationException("仍有当前程序的入站阻止规则，可能受组织策略管理，请检查 Windows 防火墙高级设置。");
            if (!after.Any(rule => (string)((dynamic)rule).Name == ruleName && IsApplicationAllow(rule, appPath, profileMask)))
                throw new InvalidOperationException("未读回预期的入站允许规则，请检查 Windows 防火墙高级设置。");

            ServiceHostLog.Write($"Firewall application rules verified. Rule={ruleName}, App={appPath}, Profile={profile}, ResolvedBlocks={resolved}");
            return new(true, ruleName, profile, resolved,
                $"已核对当前程序在{FormatProfileDisplay(profileMask)}网络上的入站允许规则，已解除 {resolved} 条冲突阻止规则在该网络上的限制。\n其它网络类型的限制保留。请重新尝试连接或取图。");
        }
        catch (Exception ex)
        {
            ServiceHostLog.Write($"Firewall application rules not verified. App={appPath}, Profile={profile}, ResolvedBlocks={resolved}: {ex}");
            return new(false, ruleName, profile, resolved,
                $"防火墙放行未完成：{ex.Message}\n已处理 {resolved} 条冲突阻止规则；已写入的变更可能保留，请核对 Windows 防火墙高级设置。");
        }
    }

    private static bool IsApplicationBlock(dynamic rule, string appPath, int profileMask) =>
        (bool)rule.Enabled && (int)rule.Direction == 1 && (int)rule.Action == 0
        && MatchesApplication((string)rule.ApplicationName, appPath)
        && (NormalizeProfiles((int)rule.Profiles) & profileMask) != 0;

    private static bool IsApplicationAllow(dynamic rule, string appPath, int profileMask, bool requireEnabled = true) =>
        (!requireEnabled || (bool)rule.Enabled) && (int)rule.Direction == 1 && (int)rule.Action == 1
        && MatchesApplication((string)rule.ApplicationName, appPath)
        && (NormalizeProfiles((int)rule.Profiles) & profileMask) == profileMask
        && (int)rule.Protocol == 256
        && IsAnyAddress((string)rule.LocalAddresses) && IsAnyAddress((string)rule.RemoteAddresses)
        && string.Equals((string)rule.InterfaceTypes, "All", StringComparison.OrdinalIgnoreCase)
        && string.IsNullOrEmpty((string)rule.ServiceName);

    private static bool IsAnyAddress(string? addresses) => string.IsNullOrEmpty(addresses) || addresses == "*";

    private static bool MatchesApplication(string? rulePath, string appPath) =>
        !string.IsNullOrWhiteSpace(rulePath)
        && string.Equals(Environment.ExpandEnvironmentVariables(rulePath), appPath, StringComparison.OrdinalIgnoreCase);

    private static int NormalizeProfiles(int profiles) => profiles == AllProfiles ? KnownProfiles : profiles;

    internal static int ResolveProfileMask(string profile, int activeProfiles) => profile.Trim().ToLowerInvariant() switch
    {
        "domain" => 1,
        "private" => 2,
        "public" => 4,
        "" when (NormalizeProfiles(activeProfiles) & KnownProfiles) != 0 => NormalizeProfiles(activeProfiles) & KnownProfiles,
        _ => throw new InvalidOperationException("未能确定要放行的网络类型，请明确选择专用或公用网络。"),
    };

    internal static string BuildFirewallRuleName(string appPath, int profileMask)
    {
        string pathHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(appPath.ToUpperInvariant())))[..16];
        return $"ColorVision Application {FormatProfileDisplay(profileMask)} ({Path.GetFileNameWithoutExtension(appPath)}) [{pathHash}]";
    }

    private static string FormatProfileArgument(int mask) => string.Join(",", new[] { (1, "domain"), (2, "private"), (4, "public") }.Where(p => (mask & p.Item1) != 0).Select(p => p.Item2));

    private static string FormatProfileDisplay(int mask) => string.Join("/", new[] { (1, "域"), (2, "专用"), (4, "公用") }.Where(p => (mask & p.Item1) != 0).Select(p => p.Item2));

    private static object CreateComObject(string progId) => Activator.CreateInstance(Type.GetTypeFromProgID(progId)
        ?? throw new InvalidOperationException($"系统没有提供 {progId} 接口。"))
        ?? throw new InvalidOperationException($"无法创建 {progId} 对象。");

    private static void ReleaseComObject(object? instance)
    {
        if (instance != null && Marshal.IsComObject(instance))
            Marshal.ReleaseComObject(instance);
    }

    private static bool IsElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    internal sealed record FirewallAllowResult(bool Success, string RuleName, string Profile, int ResolvedBlockRules, string Message);
}

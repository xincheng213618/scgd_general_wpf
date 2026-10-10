using ColorVision.SocketProtocol;
using ColorVisionServiceHost;

namespace ColorVision.UI.Tests;

public sealed class FirewallApplicationAccessTests
{
    private const string AppPath = @"C:\ColorVision\ColorVision.exe";

    [Theory]
    [InlineData(4, 4, false)]
    [InlineData(6, 2, true)]
    [InlineData(5, 1, true)]
    [InlineData(7, 3, true)]
    [InlineData(int.MaxValue, 3, true)]
    public void PublicAllowRemovesOnlyTheSelectedProfileFromBlocks(int originalProfiles, int remainingProfiles, bool enabled)
    {
        var block = new FakeRule { Profiles = originalProfiles };
        var policy = new FakePolicy(block);

        var result = Apply(policy);

        Assert.True(result.Success, result.Message);
        Assert.Equal(1, result.ResolvedBlockRules);
        Assert.Equal(remainingProfiles, block.Profiles);
        Assert.Equal(enabled, block.Enabled);
        Assert.Equal(0, block.Action);
    }

    [Fact]
    public void AllowPreservesOtherProgramsProfilesOutboundAndDisabledRules()
    {
        FakeRule[] unrelated =
        [
            new() { ApplicationName = @"D:\ColorVision\ColorVision.exe" },
            new() { ApplicationName = string.Empty },
            new() { Profiles = 2 },
            new() { Direction = 2 },
            new() { Enabled = false },
            new() { Action = 1 },
        ];
        var before = unrelated.Select(rule => (rule.Enabled, rule.Profiles)).ToArray();
        var policy = new FakePolicy(unrelated);

        var result = Apply(policy);

        Assert.True(result.Success, result.Message);
        Assert.Equal(0, result.ResolvedBlockRules);
        Assert.Equal(before, unrelated.Select(rule => (rule.Enabled, rule.Profiles)).ToArray());
        Assert.All(unrelated, rule => Assert.Contains(rule, policy.Rules));
    }

    [Fact]
    public void AllowMatchesApplicationPathsWithoutCaseSensitivity()
    {
        var block = new FakeRule { ApplicationName = AppPath.ToUpperInvariant() };
        var policy = new FakePolicy(block);

        Assert.True(Apply(policy).Success);
        Assert.False(block.Enabled);
    }

    [Fact]
    public void RepeatedAllowReusesAndReenablesItsOwnRule()
    {
        var policy = new FakePolicy();
        Assert.True(Apply(policy).Success);
        FakeRule allow = Assert.Single(policy.Rules);
        allow.Enabled = false;

        Assert.True(Apply(policy).Success);
        Assert.True(Apply(policy).Success);

        Assert.Single(policy.Rules);
        Assert.True(allow.Enabled);
        Assert.Equal(1, policy.AddCount);
    }

    [Fact]
    public void RuleNamesDistinguishInstallationsWithTheSameExecutableName()
    {
        Assert.NotEqual(FirewallCommandService.BuildFirewallRuleName(AppPath, 4),
            FirewallCommandService.BuildFirewallRuleName(@"D:\ColorVision\ColorVision.exe", 4));
        Assert.Equal(FirewallCommandService.BuildFirewallRuleName(AppPath, 4),
            FirewallCommandService.BuildFirewallRuleName(AppPath.ToUpperInvariant(), 4), ignoreCase: true);
    }

    [Fact]
    public void AddFailureLeavesBlockingRulesUnchanged()
    {
        var block = new FakeRule();
        var policy = new FakePolicy(block) { FailAdd = true };

        var result = Apply(policy);

        Assert.False(result.Success);
        Assert.Equal(0, result.ResolvedBlockRules);
        Assert.True(block.Enabled);
        Assert.Equal(4, block.Profiles);
    }

    [Fact]
    public void PartialWriteFailureReportsFailureAndCompletedRuleCount()
    {
        var first = new FakeRule();
        var denied = new FakeRule { DenyChanges = true };
        var policy = new FakePolicy(first, denied);

        var result = Apply(policy);

        Assert.False(result.Success);
        Assert.Equal(1, result.ResolvedBlockRules);
        Assert.False(first.Enabled);
        Assert.True(denied.Enabled);
        Assert.Contains("变更可能保留", result.Message);
    }

    [Fact]
    public void ReadbackMustConfirmThatBlockingRuleWasChanged()
    {
        var block = new FakeRule { IgnoreChanges = true };
        var policy = new FakePolicy(block);

        var result = Apply(policy);

        Assert.False(result.Success);
        Assert.Contains("仍有", result.Message);
    }

    [Fact]
    public void ReadbackMustFindTheNewAllowRule()
    {
        var policy = new FakePolicy { IgnoreAdd = true };

        var result = Apply(policy);

        Assert.False(result.Success);
        Assert.Contains("未读回", result.Message);
    }

    [Theory]
    [InlineData("publci", 4)]
    [InlineData("all", 4)]
    [InlineData("", 0)]
    public void AmbiguousProfileNeverFallsBackToOpeningAllNetworks(string profile, int activeProfiles)
    {
        Assert.Throws<InvalidOperationException>(() => FirewallCommandService.ResolveProfileMask(profile, activeProfiles));
    }

    [Fact]
    public void ExplicitProfileDoesNotInheritOtherActiveNetworks()
    {
        Assert.Equal(4, FirewallCommandService.ResolveProfileMask("public", 7));
        Assert.Equal(6, FirewallCommandService.ResolveProfileMask("", 6));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void ClientRejectsSuccessFromOldServiceIfRulesRemainBlockedOrUnknown(bool canAllow, bool hasAllowRule)
    {
        var status = new FirewallStatus("unverified", "rule details", canAllow, hasAllowRule);

        var result = SocketFirewallService.VerifyAllowResult("service success", status);

        Assert.False(result.Success);
        Assert.Contains("核验未通过", result.Message);
    }

    [Fact]
    public void ClientReportsRuleVerificationWithoutClaimingConnectivity()
    {
        var result = SocketFirewallService.VerifyAllowResult("service success", new FirewallStatus("allowed", "", false, true));
        Assert.True(result.Success);
        Assert.Contains("重新尝试连接或取图", result.Message);
    }

    private static FirewallCommandService.FirewallAllowResult Apply(FakePolicy policy) =>
        FirewallCommandService.ConfigureApplicationRules(AppPath, 4, () => policy.Rules.Cast<object>().ToArray(), policy.AddAllow);

    private sealed class FakePolicy(params FakeRule[] rules)
    {
        public List<FakeRule> Rules { get; } = [.. rules];
        public bool FailAdd { get; init; }
        public bool IgnoreAdd { get; init; }
        public int AddCount { get; private set; }

        public void AddAllow(string name, string appPath, int profiles)
        {
            if (FailAdd)
                throw new UnauthorizedAccessException("test permission failure");
            AddCount++;
            if (!IgnoreAdd)
                Rules.Add(new FakeRule { Name = name, ApplicationName = appPath, Profiles = profiles, Action = 1 });
        }
    }

    // Public for the runtime binder used by the real COM rule code.
    public sealed class FakeRule
    {
        private bool _enabled = true;
        private int _profiles = 4;
        public string Name { get; init; } = "Existing application rule";
        public string ApplicationName { get; init; } = AppPath;
        public int Direction { get; init; } = 1;
        public int Action { get; init; }
        public int Protocol { get; init; } = 256;
        public string LocalAddresses { get; init; } = "*";
        public string RemoteAddresses { get; init; } = "*";
        public string InterfaceTypes { get; init; } = "All";
        public string ServiceName { get; init; } = string.Empty;
        public bool DenyChanges { get; init; }
        public bool IgnoreChanges { get; init; }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (DenyChanges) throw new UnauthorizedAccessException("test policy lock");
                if (!IgnoreChanges) _enabled = value;
            }
        }

        public int Profiles
        {
            get => _profiles;
            set
            {
                if (DenyChanges) throw new UnauthorizedAccessException("test policy lock");
                if (!IgnoreChanges) _profiles = value;
            }
        }
    }
}

using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatStateStoreReadFailureTests
{
    [Theory]
    [InlineData(FileShare.None)]
    [InlineData(FileShare.Delete)]
    public void LockedPrimaryIsNotReplacedByAnOlderBackup(FileShare sharing)
    {
        using var fixture = new StateStoreFixture();
        fixture.SaveDraft("older backup");
        fixture.SaveDraft("latest primary");
        var primary = File.ReadAllBytes(fixture.Store.StateFilePath);
        var backup = File.ReadAllBytes(fixture.Store.BackupStateFilePath);

        Exception? error;
        using (new FileStream(fixture.Store.StateFilePath, FileMode.Open, FileAccess.Read, sharing))
            error = Record.Exception(() => fixture.Store.Load());

        Assert.Equal(primary, File.ReadAllBytes(fixture.Store.StateFilePath));
        Assert.Equal(backup, File.ReadAllBytes(fixture.Store.BackupStateFilePath));
        Assert.IsAssignableFrom<IOException>(error);
        Assert.Equal("latest primary", Assert.Single(fixture.Store.Load().Conversations).DraftText);
    }

    [Theory]
    [InlineData(FileShare.None)]
    [InlineData(FileShare.Delete)]
    public void LockedTemporarySnapshotIsNotDeletedOrSkipped(FileShare sharing)
    {
        using var fixture = new StateStoreFixture();
        fixture.SaveDraft("older primary");
        fixture.WriteTemporaryDraft("latest temporary snapshot");
        var temporary = File.ReadAllBytes(fixture.Store.TemporaryStateFilePath);
        var primary = File.ReadAllBytes(fixture.Store.StateFilePath);

        Exception? error;
        using (new FileStream(fixture.Store.TemporaryStateFilePath, FileMode.Open, FileAccess.Read, sharing))
            error = Record.Exception(() => fixture.Store.Load());

        Assert.True(File.Exists(fixture.Store.TemporaryStateFilePath));
        Assert.Equal(temporary, File.ReadAllBytes(fixture.Store.TemporaryStateFilePath));
        Assert.Equal(primary, File.ReadAllBytes(fixture.Store.StateFilePath));
        Assert.IsAssignableFrom<IOException>(error);
        Assert.Equal("latest temporary snapshot", Assert.Single(fixture.Store.Load().Conversations).DraftText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavingDoesNotReplaceAnUnreadablePrimaryAndCanRetryAfterUnlock(bool asynchronous)
    {
        using var fixture = new StateStoreFixture();
        fixture.SaveDraft("older backup");
        fixture.SaveDraft("latest primary");
        var primary = File.ReadAllBytes(fixture.Store.StateFilePath);
        var backup = File.ReadAllBytes(fixture.Store.BackupStateFilePath);

        Exception? error;
        using (new FileStream(fixture.Store.StateFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete))
            error = await Record.ExceptionAsync(() => fixture.SaveDraftAsync("new draft", asynchronous));

        Assert.Equal(primary, File.ReadAllBytes(fixture.Store.StateFilePath));
        Assert.Equal(backup, File.ReadAllBytes(fixture.Store.BackupStateFilePath));
        Assert.IsAssignableFrom<IOException>(error);

        await fixture.SaveDraftAsync("new draft", asynchronous);
        Assert.Equal("new draft", Assert.Single(fixture.Store.Load().Conversations).DraftText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedTemporaryFileOpenDoesNotDeleteTheExistingRecoverySnapshot(bool asynchronous)
    {
        using var fixture = new StateStoreFixture();
        fixture.SaveDraft("older primary");
        fixture.WriteTemporaryDraft("recoverable draft");
        var primary = File.ReadAllBytes(fixture.Store.StateFilePath);
        var temporary = File.ReadAllBytes(fixture.Store.TemporaryStateFilePath);

        using (new FileStream(fixture.Store.TemporaryStateFilePath, FileMode.Open, FileAccess.Read, FileShare.Delete))
            await Assert.ThrowsAnyAsync<IOException>(() => fixture.SaveDraftAsync("new draft", asynchronous));

        Assert.True(File.Exists(fixture.Store.TemporaryStateFilePath));
        Assert.Equal(temporary, File.ReadAllBytes(fixture.Store.TemporaryStateFilePath));
        Assert.Equal(primary, File.ReadAllBytes(fixture.Store.StateFilePath));
        Assert.Equal("recoverable draft", Assert.Single(fixture.Store.Load().Conversations).DraftText);

        await fixture.SaveDraftAsync("new draft", asynchronous);
        Assert.Equal("new draft", Assert.Single(fixture.Store.Load().Conversations).DraftText);
    }

    [Fact]
    public void AnInaccessibleStateFileIsNotTreatedAsMissing()
    {
        using var fixture = new StateStoreFixture();
        Directory.CreateDirectory(fixture.Store.StateFilePath);

        Assert.Throws<UnauthorizedAccessException>(() => fixture.Store.Load());
        Assert.NotEqual(CopilotChatStateLoadSource.Fresh, fixture.Store.LastLoadStatus.Source);
    }

    private sealed class StateStoreFixture : IDisposable
    {
        private static readonly string FixtureDirectory = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(), nameof(CopilotChatStateStoreReadFailureTests)));

        private readonly string _root = Path.Combine(FixtureDirectory, Guid.NewGuid().ToString("N"));
        private readonly CopilotChatState _state = new()
        {
            ActiveProfileId = "profile",
            Conversations = [CopilotConversationRecord.CreateEmpty("profile", "Profile")],
        };

        public CopilotChatStateStore Store { get; }

        public StateStoreFixture()
        {
            Store = new CopilotChatStateStore(_root);
            _state.ActiveConversationId = _state.Conversations[0].Id;
        }

        public void SaveDraft(string draft)
        {
            _state.Conversations[0].DraftText = draft;
            Store.Save(_state);
        }

        public Task SaveDraftAsync(string draft, bool asynchronous)
        {
            _state.Conversations[0].DraftText = draft;
            if (asynchronous)
                return Store.SaveSerializedAsync(Store.Serialize(_state));

            Store.Save(_state);
            return Task.CompletedTask;
        }

        public void WriteTemporaryDraft(string draft)
        {
            _state.Conversations[0].DraftText = draft;
            File.WriteAllText(Store.TemporaryStateFilePath, Store.Serialize(_state));
            File.SetLastWriteTimeUtc(Store.StateFilePath, DateTime.UtcNow.AddMinutes(-1));
        }

        public void Dispose()
        {
            var fullPath = Path.GetFullPath(_root);
            if (!fullPath.StartsWith(FixtureDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The fixture directory is outside its temporary root.");
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
    }
}

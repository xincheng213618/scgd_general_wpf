using ProjectARVRPro.Process;
using ProjectARVRPro.Services;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class SwitchPgAcknowledgementTests
{
    private static readonly DateTime SentAt = new(2026, 11, 20, 8, 0, 0);

    [Fact]
    public void AckRequiresACompletedWriteAndTheOriginalStillActiveConnection()
    {
        using var original = new MemoryStream();
        using var other = new MemoryStream();
        var process = new ProcessMeta();
        var acknowledgement = new SwitchPgAcknowledgement();

        Assert.Null(acknowledgement.TryConsume(original, original, 0, process));

        var request = acknowledgement.Prepare(original, 0, process);
        Assert.False(acknowledgement.IsAwaiting(original));
        Assert.Null(acknowledgement.TryConsume(original, original, 0, process));

        acknowledgement.MarkSent(request, SentAt);
        Assert.True(acknowledgement.IsAwaiting(original));
        Assert.False(acknowledgement.IsAwaiting(other));
        Assert.Null(acknowledgement.TryConsume(other, original, 0, process));
        Assert.Null(acknowledgement.TryConsume(original, other, 0, process));
        Assert.Null(acknowledgement.TryConsume(other, other, 0, process));
        Assert.Equal(SentAt, acknowledgement.TryConsume(original, original, 0, process));
        Assert.False(acknowledgement.IsAwaiting(original));
    }

    [Fact]
    public async Task ConcurrentAcknowledgementsConsumeTheSameRequestOnlyOnce()
    {
        using var stream = new MemoryStream();
        var process = new ProcessMeta();
        var acknowledgement = new SwitchPgAcknowledgement();
        var request = acknowledgement.Prepare(stream, 1, process);
        acknowledgement.MarkSent(request, SentAt);

        Task<DateTime?> first = Task.Run(() => acknowledgement.TryConsume(stream, stream, 1, process));
        Task<DateTime?> second = Task.Run(() => acknowledgement.TryConsume(stream, stream, 1, process));
        DateTime? accepted = Assert.Single((await Task.WhenAll(first, second)).Where(sentAt => sentAt.HasValue));

        Assert.Equal(SentAt, accepted);
        acknowledgement.MarkSent(request, SentAt.AddSeconds(1));
        Assert.Null(acknowledgement.TryConsume(stream, stream, 1, process));
    }

    [Fact]
    public void LateWriteCompletionCannotArmAReplacedOrClearedRequest()
    {
        using var original = new MemoryStream();
        using var replacement = new MemoryStream();
        var process = new ProcessMeta();
        var acknowledgement = new SwitchPgAcknowledgement();
        var first = acknowledgement.Prepare(original, 0, process);
        var second = acknowledgement.Prepare(replacement, 0, process);

        acknowledgement.MarkSent(first, SentAt);
        Assert.False(acknowledgement.IsAwaiting(original));
        Assert.False(acknowledgement.IsAwaiting(replacement));
        Assert.Null(acknowledgement.TryConsume(replacement, replacement, 0, process));

        acknowledgement.MarkSent(second, SentAt.AddSeconds(1));
        acknowledgement.MarkSent(first, SentAt.AddSeconds(2));
        Assert.Equal(SentAt.AddSeconds(1), acknowledgement.TryConsume(replacement, replacement, 0, process));

        var canceled = acknowledgement.Prepare(replacement, 1, process);
        acknowledgement.Clear();
        acknowledgement.MarkSent(canceled, SentAt.AddSeconds(3));
        Assert.Null(acknowledgement.TryConsume(replacement, replacement, 1, process));
    }

    [Fact]
    public void AckCannotStartADifferentStepOrEditedTemplate()
    {
        using var stream = new MemoryStream();
        var process = new ProcessMeta { FlowTemplate = "First" };
        var other = new ProcessMeta { FlowTemplate = "First" };
        var acknowledgement = new SwitchPgAcknowledgement();
        var request = acknowledgement.Prepare(stream, 1, process);
        acknowledgement.MarkSent(request, SentAt);

        Assert.Null(acknowledgement.TryConsume(stream, stream, 2, process));
        Assert.Null(acknowledgement.TryConsume(stream, stream, 1, other));
        process.FlowTemplate = "Changed";
        Assert.Null(acknowledgement.TryConsume(stream, stream, 1, process));

        var replacement = acknowledgement.Prepare(stream, 1, process);
        acknowledgement.MarkSent(replacement, SentAt.AddSeconds(1));
        Assert.Equal(SentAt.AddSeconds(1), acknowledgement.TryConsume(stream, stream, 1, process));
    }
}

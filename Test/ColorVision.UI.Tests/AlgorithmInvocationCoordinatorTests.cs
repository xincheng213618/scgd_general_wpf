using ColorVision.Algorithms;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmInvocationCoordinatorTests
{
    [Fact]
    public void SameDocumentRevisionIsLatestWinsAcrossOwners()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 17);
        using CancellationTokenSource previewCancellation = new();
        using CancellationTokenSource analysisCancellation = new();

        AlgorithmInvocationClaim preview = coordinator.Claim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            previewCancellation);
        AlgorithmInvocationClaim analysis = coordinator.Claim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            analysisCancellation);

        Assert.True(previewCancellation.IsCancellationRequested);
        Assert.False(analysisCancellation.IsCancellationRequested);
        Assert.False(coordinator.IsCurrent(preview));
        Assert.True(coordinator.IsCurrent(analysis));
        Assert.False(coordinator.TryRelease(preview));
        Assert.True(coordinator.IsCurrent(analysis));
    }

    [Fact]
    public void DifferentDocumentsAndRevisionsDoNotCancelEachOther()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        Guid firstDocument = Guid.NewGuid();
        Guid secondDocument = Guid.NewGuid();
        AlgorithmInvocationScope firstRevision = new(firstDocument, 3);
        AlgorithmInvocationScope nextRevision = new(firstDocument, 4);
        AlgorithmInvocationScope otherDocument = new(secondDocument, 3);
        using CancellationTokenSource firstCancellation = new();
        using CancellationTokenSource nextCancellation = new();
        using CancellationTokenSource otherCancellation = new();

        AlgorithmInvocationClaim first = coordinator.Claim(firstRevision, Guid.NewGuid(), Guid.NewGuid(), firstCancellation);
        AlgorithmInvocationClaim next = coordinator.Claim(nextRevision, Guid.NewGuid(), Guid.NewGuid(), nextCancellation);
        AlgorithmInvocationClaim other = coordinator.Claim(otherDocument, Guid.NewGuid(), Guid.NewGuid(), otherCancellation);

        Assert.False(firstCancellation.IsCancellationRequested);
        Assert.False(nextCancellation.IsCancellationRequested);
        Assert.False(otherCancellation.IsCancellationRequested);
        Assert.True(coordinator.IsCurrent(first));
        Assert.True(coordinator.IsCurrent(next));
        Assert.True(coordinator.IsCurrent(other));

        using CancellationTokenSource replacementCancellation = new();
        AlgorithmInvocationClaim replacement = coordinator.Claim(
            firstRevision,
            Guid.NewGuid(),
            Guid.NewGuid(),
            replacementCancellation);
        Assert.True(firstCancellation.IsCancellationRequested);
        Assert.False(nextCancellation.IsCancellationRequested);
        Assert.False(otherCancellation.IsCancellationRequested);
        Assert.True(coordinator.IsCurrent(replacement));
        Assert.True(coordinator.IsCurrent(next));
        Assert.True(coordinator.IsCurrent(other));
    }

    [Fact]
    public void CompleteRunDetachesCancellationAndReleaseCannotAffectReplacement()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 9);
        using CancellationTokenSource completedCancellation = new();
        AlgorithmInvocationClaim completed = coordinator.Claim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            completedCancellation);

        Assert.True(coordinator.CompleteRun(completed, completedCancellation));
        Assert.True(coordinator.TryRelease(completed));
        Assert.False(completedCancellation.IsCancellationRequested);

        using CancellationTokenSource oldCancellation = new();
        using CancellationTokenSource currentCancellation = new();
        AlgorithmInvocationClaim old = coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid(), oldCancellation);
        AlgorithmInvocationClaim current = coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid(), currentCancellation);
        Assert.True(oldCancellation.IsCancellationRequested);
        Assert.False(coordinator.CompleteRun(old, oldCancellation));
        Assert.False(coordinator.TryRelease(old));
        Assert.False(currentCancellation.IsCancellationRequested);
        Assert.True(coordinator.TryRelease(current));
        Assert.True(currentCancellation.IsCancellationRequested);
        Assert.False(coordinator.TryGetCurrent(scope, out _));
    }

    [Fact]
    public void CancellationCallbackFailureDoesNotCorruptNewOwnership()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 1);
        using CancellationTokenSource throwingCancellation = new();
        using CancellationTokenRegistration registration = throwingCancellation.Token.Register(
            static () => throw new InvalidOperationException("expected cancellation callback failure"));
        AlgorithmInvocationClaim old = coordinator.Claim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            throwingCancellation);

        AlgorithmInvocationClaim current = coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid());

        Assert.True(throwingCancellation.IsCancellationRequested);
        Assert.False(coordinator.IsCurrent(old));
        Assert.True(coordinator.IsCurrent(current));
        Assert.True(coordinator.TryRelease(current));
    }

    [Fact]
    public void InvalidateDocumentCancelsOnlyThatDocumentsClaims()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        Guid invalidatedDocument = Guid.NewGuid();
        Guid retainedDocument = Guid.NewGuid();
        using CancellationTokenSource first = new();
        using CancellationTokenSource second = new();
        using CancellationTokenSource retained = new();
        AlgorithmInvocationClaim retainedClaim = coordinator.Claim(
            new AlgorithmInvocationScope(retainedDocument, 2),
            Guid.NewGuid(),
            Guid.NewGuid(),
            retained);
        coordinator.Claim(new AlgorithmInvocationScope(invalidatedDocument, 1), Guid.NewGuid(), Guid.NewGuid(), first);
        coordinator.Claim(new AlgorithmInvocationScope(invalidatedDocument, 2), Guid.NewGuid(), Guid.NewGuid(), second);

        Assert.Equal(2, coordinator.InvalidateDocument(invalidatedDocument));
        Assert.True(first.IsCancellationRequested);
        Assert.True(second.IsCancellationRequested);
        Assert.False(retained.IsCancellationRequested);
        Assert.True(coordinator.IsCurrent(retainedClaim));
    }

    [Fact]
    public void RejectedOrThrowingAcceptanceRestoresThePreviousClaimWithoutCancellation()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 5);
        using CancellationTokenSource previousCancellation = new();
        using CancellationTokenSource rejectedCancellation = new();
        using CancellationTokenSource throwingCancellation = new();
        AlgorithmInvocationClaim previous = coordinator.Claim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            previousCancellation);

        Assert.False(coordinator.TryClaim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            rejectedCancellation,
            _ => false,
            out _));
        Assert.True(coordinator.IsCurrent(previous));
        Assert.False(previousCancellation.IsCancellationRequested);
        Assert.False(rejectedCancellation.IsCancellationRequested);

        Assert.Throws<InvalidOperationException>(() => coordinator.TryClaim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            throwingCancellation,
            _ => throw new InvalidOperationException("injected acceptance failure"),
            out _));
        Assert.True(coordinator.IsCurrent(previous));
        Assert.False(previousCancellation.IsCancellationRequested);
        Assert.False(throwingCancellation.IsCancellationRequested);
    }

    [Fact]
    public void ThrowingReleasePublicationRetainsClaimAndCancellationAttachment()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 7);
        using CancellationTokenSource cancellation = new();
        AlgorithmInvocationClaim claim = coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid(), cancellation);

        Assert.Throws<InvalidOperationException>(() => coordinator.TryRelease(
            claim,
            () => throw new InvalidOperationException("injected publication failure")));

        Assert.True(coordinator.IsCurrent(claim));
        Assert.False(cancellation.IsCancellationRequested);
        Assert.True(coordinator.TryRelease(claim));
        Assert.True(cancellation.IsCancellationRequested);
    }

    [Fact]
    public void RejectedOuterClaimWithNestedNewerCancelsEveryDetachedRunExactlyOnce()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 11);
        using CancellationTokenSource previousCancellation = new();
        using CancellationTokenSource candidateCancellation = new();
        using CancellationTokenSource newerCancellation = new();
        int previousCancellationCount = 0;
        int candidateCancellationCount = 0;
        using CancellationTokenRegistration previousRegistration = previousCancellation.Token.Register(
            () => Interlocked.Increment(ref previousCancellationCount));
        using CancellationTokenRegistration candidateRegistration = candidateCancellation.Token.Register(
            () => Interlocked.Increment(ref candidateCancellationCount));
        AlgorithmInvocationClaim previous = coordinator.Claim(
            scope, Guid.NewGuid(), Guid.NewGuid(), previousCancellation);
        AlgorithmInvocationClaim newer = default;

        bool accepted = coordinator.TryClaim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            candidateCancellation,
            _ =>
            {
                newer = coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid(), newerCancellation);
                return false;
            },
            out AlgorithmInvocationClaim returned);

        Assert.False(accepted);
        Assert.Equal(default, returned);
        Assert.False(coordinator.IsCurrent(previous));
        Assert.True(coordinator.IsCurrent(newer));
        Assert.Equal(1, previousCancellationCount);
        Assert.Equal(1, candidateCancellationCount);
        Assert.False(newerCancellation.IsCancellationRequested);
    }

    [Fact]
    public void ThrowingOuterClaimAfterReentrantScopeInvalidationCancelsDetachedPreviousExactlyOnce()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 12);
        using CancellationTokenSource previousCancellation = new();
        using CancellationTokenSource candidateCancellation = new();
        int previousCancellationCount = 0;
        int candidateCancellationCount = 0;
        using CancellationTokenRegistration previousRegistration = previousCancellation.Token.Register(
            () => Interlocked.Increment(ref previousCancellationCount));
        using CancellationTokenRegistration candidateRegistration = candidateCancellation.Token.Register(
            () => Interlocked.Increment(ref candidateCancellationCount));
        coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid(), previousCancellation);

        Assert.Throws<InvalidOperationException>(() => coordinator.TryClaim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            candidateCancellation,
            _ =>
            {
                Assert.True(coordinator.InvalidateScope(scope));
                throw new InvalidOperationException("injected reentrant invalidation");
            },
            out _));

        Assert.False(coordinator.TryGetCurrent(scope, out _));
        Assert.Equal(1, previousCancellationCount);
        Assert.Equal(1, candidateCancellationCount);
    }

    [Fact]
    public void AcceptedCallbackThatInstallsNewerClaimCannotReturnAStaleSuccessfulClaim()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 13);
        using CancellationTokenSource previousCancellation = new();
        using CancellationTokenSource candidateCancellation = new();
        using CancellationTokenSource newerCancellation = new();
        AlgorithmInvocationClaim previous = coordinator.Claim(
            scope, Guid.NewGuid(), Guid.NewGuid(), previousCancellation);
        AlgorithmInvocationClaim newer = default;

        bool accepted = coordinator.TryClaim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            candidateCancellation,
            _ =>
            {
                newer = coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid(), newerCancellation);
                return true;
            },
            out AlgorithmInvocationClaim returned);

        Assert.False(accepted);
        Assert.Equal(default, returned);
        Assert.False(coordinator.IsCurrent(previous));
        Assert.True(coordinator.IsCurrent(newer));
        Assert.True(previousCancellation.IsCancellationRequested);
        Assert.True(candidateCancellation.IsCancellationRequested);
        Assert.False(newerCancellation.IsCancellationRequested);
    }

    [Fact]
    public void CancellationOfTheSupersededRunCannotMakeTryClaimReturnAStaleSuccessfulClaim()
    {
        AlgorithmInvocationCoordinator coordinator = new();
        AlgorithmInvocationScope scope = new(Guid.NewGuid(), 14);
        using CancellationTokenSource previousCancellation = new();
        using CancellationTokenSource candidateCancellation = new();
        using CancellationTokenSource newerCancellation = new();
        AlgorithmInvocationClaim previous = coordinator.Claim(
            scope, Guid.NewGuid(), Guid.NewGuid(), previousCancellation);
        AlgorithmInvocationClaim newer = default;
        using CancellationTokenRegistration registration = previousCancellation.Token.Register(() =>
        {
            newer = coordinator.Claim(scope, Guid.NewGuid(), Guid.NewGuid(), newerCancellation);
        });

        bool accepted = coordinator.TryClaim(
            scope,
            Guid.NewGuid(),
            Guid.NewGuid(),
            candidateCancellation,
            accept: null,
            out AlgorithmInvocationClaim returned);

        Assert.False(accepted);
        Assert.Equal(default, returned);
        Assert.False(coordinator.IsCurrent(previous));
        Assert.True(coordinator.IsCurrent(newer));
        Assert.True(previousCancellation.IsCancellationRequested);
        Assert.True(candidateCancellation.IsCancellationRequested);
        Assert.False(newerCancellation.IsCancellationRequested);
    }
}

using ColorVision.Engine;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI.ServiceHost;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class PhysicalCameraCreationBatchTests
{
    [Fact]
    public async Task BatchOfNewCamerasRestartsRegistryOnlyOnceWithoutWaitingForUserInput()
    {
        List<string> restartedServices = [];
        PhysicalCameraCreationBatch batch = new(name =>
        {
            restartedServices.Add(name);
            return Task.FromResult(new ServiceHostResponse { Success = true });
        });
        batch.RecordSaved(true, new SysResourceModel { Id = 1, Code = "camera-1" }, 1);
        batch.RecordSaved(true, new SysResourceModel { Id = 2, Code = "camera-2" }, 1);
        batch.RecordSaved(true, new SysResourceModel { Id = 1, Code = "CAMERA-1" }, 1);

        Task<bool> activation = batch.ActivateAsync();

        Assert.True(await activation);
        Assert.Same(activation, batch.ActivateAsync());
        Assert.Equal(["RegistrationCenterService"], restartedServices);
    }

    [Theory]
    [InlineData(false, 1, 1)] // Existing configured camera.
    [InlineData(true, -2, 1)] // Local configuration.
    [InlineData(true, 0, 1)] // No persisted identity.
    [InlineData(true, 1, -1)] // Save failed.
    [InlineData(true, 1, 0)] // No row saved.
    public async Task ExistingOfflineOrUnsavedCameraDoesNotRestartServices(bool requiresCreation, int id, int saveResult)
    {
        int calls = 0;
        PhysicalCameraCreationBatch batch = new(_ =>
        {
            calls++;
            return Task.FromResult(new ServiceHostResponse { Success = true });
        });
        batch.RecordSaved(requiresCreation, new SysResourceModel { Id = id, Code = "camera-1" }, saveResult);

        Assert.True(await batch.ActivateAsync());
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task EmptyOrCancelledImportDoesNotRestartServices()
    {
        int calls = 0;
        PhysicalCameraCreationBatch batch = new(_ =>
        {
            calls++;
            return Task.FromResult(new ServiceHostResponse { Success = true });
        });

        Assert.True(await batch.ActivateAsync());
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRestartIsReportedWithoutThrowingAndReleasesGateForNextCreation(bool throws)
    {
        PhysicalCameraCreationBatch failed = new(_ => throws
            ? Task.FromException<ServiceHostResponse>(new IOException("service host unavailable"))
            : Task.FromResult(new ServiceHostResponse { Success = false, Message = "service not installed" }));
        failed.RecordSaved(true, new SysResourceModel { Id = 1, Code = "camera-1" }, 1);

        Assert.False(await failed.ActivateAsync());

        PhysicalCameraCreationBatch next = new(_ => Task.FromResult(new ServiceHostResponse { Success = true }));
        next.RecordSaved(true, new SysResourceModel { Id = 2, Code = "camera-2" }, 1);
        Assert.True(await next.ActivateAsync().WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ConcurrentCreationBatchesDoNotOverlapWindowsServiceRestarts()
    {
        TaskCompletionSource<ServiceHostResponse> firstRestart = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PhysicalCameraCreationBatch first = new(_ => firstRestart.Task);
        first.RecordSaved(true, new SysResourceModel { Id = 1, Code = "camera-1" }, 1);
        bool secondStarted = false;
        PhysicalCameraCreationBatch second = new(_ =>
        {
            secondStarted = true;
            return Task.FromResult(new ServiceHostResponse { Success = true });
        });
        second.RecordSaved(true, new SysResourceModel { Id = 2, Code = "camera-2" }, 1);

        Task<bool> firstActivation = first.ActivateAsync();
        Task<bool> secondActivation = second.ActivateAsync();
        try
        {
            Assert.False(secondStarted);
            Assert.False(secondActivation.IsCompleted);
        }
        finally
        {
            firstRestart.TrySetResult(new ServiceHostResponse { Success = true });
            await Task.WhenAll(firstActivation, secondActivation).WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.True(secondStarted);
    }
}

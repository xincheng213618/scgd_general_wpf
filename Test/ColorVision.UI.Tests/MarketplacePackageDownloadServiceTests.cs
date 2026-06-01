using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Marketplace;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests
{
    public sealed class MarketplacePackageDownloadServiceTests : IDisposable
    {
        private readonly string _downloadDirectory = Path.Combine(Path.GetTempPath(), nameof(MarketplacePackageDownloadServiceTests), Guid.NewGuid().ToString("N"));

        [Fact]
        public async Task EnsurePackageAvailableAsync_UsesCachedPackageBeforeStartingDownload()
        {
            string cachedPath = Path.Combine(_downloadDirectory, "PluginA-1.0.0.cvxp");
            Directory.CreateDirectory(_downloadDirectory);
            File.WriteAllBytes(cachedPath, new byte[] { 1, 2, 3 });

            var client = new FakeMarketplacePackageClient
            {
                ExistingFileResolver = (_, _, _, _) => cachedPath,
            };
            var downloader = new FakeMarketplacePackageDownloader();
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            string? result = await service.EnsurePackageAvailableAsync(CreateRequest("PluginA", "1.0.0"), showFailureDialog: false);

            Assert.Equal(cachedPath, result);
            Assert.Empty(downloader.Invocations);
            Assert.Equal(0, ui.ShowDownloadWindowCalls);
            Assert.Empty(installer.InstallCalls);
        }

        [Fact]
        public async Task InstallPackageAsync_DoesNotInstallWhenHashVerificationFails()
        {
            var client = new FakeMarketplacePackageClient
            {
                VerifyFileHashResult = false,
            };
            var downloader = new FakeMarketplacePackageDownloader
            {
                DownloadFactory = invocation => new DownloadTask
                {
                    Status = DownloadStatus.Completed,
                    SavePath = Path.Combine(invocation.DownloadDirectory, invocation.FileName),
                },
            };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            bool installed = await service.InstallPackageAsync(CreateRequest("PluginA", "1.0.0", "expected-hash"));

            Assert.False(installed);
            Assert.Single(downloader.Invocations);
            Assert.Empty(installer.InstallCalls);
            Assert.Single(ui.Errors);
            Assert.Equal(1, client.VerifyFileHashCalls);
        }

        [Fact]
        public async Task InstallPackageAsync_ReturnsFalseWhenDownloadFails()
        {
            var client = new FakeMarketplacePackageClient();
            var downloader = new FakeMarketplacePackageDownloader
            {
                DownloadFactory = invocation => new DownloadTask
                {
                    Status = DownloadStatus.Failed,
                    ErrorMessage = "network down",
                    SavePath = Path.Combine(invocation.DownloadDirectory, invocation.FileName),
                },
            };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            bool installed = await service.InstallPackageAsync(CreateRequest("PluginA", "1.0.0"));

            Assert.False(installed);
            Assert.Single(downloader.Invocations);
            Assert.Empty(installer.InstallCalls);
            Assert.Single(ui.Warnings);
            Assert.Equal("network down", ui.Warnings[0]);
            Assert.Equal(0, client.VerifyFileHashCalls);
        }

        [Fact]
        public async Task EnsurePackagesAvailableAsync_DeduplicatesDuplicateRequests()
        {
            var client = new FakeMarketplacePackageClient
            {
                VerifyFileHashResult = true,
            };
            var downloader = new FakeMarketplacePackageDownloader
            {
                DownloadFactory = invocation => new DownloadTask
                {
                    Status = DownloadStatus.Completed,
                    SavePath = Path.Combine(invocation.DownloadDirectory, invocation.FileName),
                },
            };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            IReadOnlyList<string> paths = await service.EnsurePackagesAvailableAsync(
                new[]
                {
                    CreateRequest("PluginA", "1.0.0", "hash-a"),
                    CreateRequest("PluginA", "1.0.0", "hash-a"),
                    CreateRequest("PluginB", "2.0.0", "hash-b"),
                },
                showFailureDialog: false);

            Assert.Equal(2, downloader.Invocations.Count);
            Assert.Equal(2, paths.Count);
            Assert.Contains(Path.Combine(_downloadDirectory, "PluginA-1.0.0.cvxp"), paths);
            Assert.Contains(Path.Combine(_downloadDirectory, "PluginB-2.0.0.cvxp"), paths);
        }

        [Fact]
        public async Task EnsurePackageAvailableAsync_CancelsPendingDownload()
        {
            var client = new FakeMarketplacePackageClient();
            var downloader = new FakeMarketplacePackageDownloader
            {
                CompleteImmediately = false,
            };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);
            using var cancellationTokenSource = new CancellationTokenSource();

            Task<string?> packageTask = service.EnsurePackageAvailableAsync(
                CreateRequest("PluginA", "1.0.0"),
                showFailureDialog: false,
                cancellationToken: cancellationTokenSource.Token);

            Assert.Single(downloader.Invocations);

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => packageTask);
            Assert.Single(downloader.CancelledTasks);
            Assert.Empty(installer.InstallCalls);
            Assert.Empty(ui.Warnings);
            Assert.Empty(ui.Errors);
        }

        [Fact]
        public async Task EnsurePackagesAvailableAsync_CancelledBatchCancelsStartedDownloads()
        {
            var client = new FakeMarketplacePackageClient();
            var downloader = new FakeMarketplacePackageDownloader
            {
                CompleteImmediately = false,
            };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);
            using var cancellationTokenSource = new CancellationTokenSource();

            Task<IReadOnlyList<string>> packagesTask = service.EnsurePackagesAvailableAsync(
                new[]
                {
                    CreateRequest("PluginA", "1.0.0"),
                    CreateRequest("PluginB", "2.0.0"),
                },
                showFailureDialog: false,
                cancellationToken: cancellationTokenSource.Token);

            Assert.Equal(2, downloader.Invocations.Count);

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => packagesTask);
            Assert.Equal(2, downloader.CancelledTasks.Count);
            Assert.Empty(installer.InstallCalls);
        }

        [Fact]
        public async Task EnsurePackagesAvailableAsync_ReturnsSuccessfulPackagesWhenOneDownloadFails()
        {
            var client = new FakeMarketplacePackageClient();
            var downloader = new FakeMarketplacePackageDownloader
            {
                DownloadFactory = invocation => new DownloadTask
                {
                    Status = invocation.FileName.StartsWith("PluginA", StringComparison.OrdinalIgnoreCase)
                        ? DownloadStatus.Completed
                        : DownloadStatus.Failed,
                    ErrorMessage = "network down",
                    SavePath = Path.Combine(invocation.DownloadDirectory, invocation.FileName),
                },
            };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            IReadOnlyList<string> paths = await service.EnsurePackagesAvailableAsync(
                new[]
                {
                    CreateRequest("PluginA", "1.0.0"),
                    CreateRequest("PluginB", "2.0.0"),
                },
                showFailureDialog: false);

            Assert.Single(paths);
            Assert.Equal(Path.Combine(_downloadDirectory, "PluginA-1.0.0.cvxp"), paths[0]);
            Assert.Equal(2, downloader.Invocations.Count);
            Assert.Empty(ui.Warnings);
            Assert.Empty(installer.InstallCalls);
        }

        public void Dispose()
        {
            if (Directory.Exists(_downloadDirectory))
            {
                Directory.Delete(_downloadDirectory, recursive: true);
            }
        }

        private static MarketplacePackageDownloadService CreateService(
            FakeMarketplacePackageClient client,
            FakeMarketplacePackageDownloader downloader,
            FakeMarketplacePackageInstaller installer,
            FakeMarketplacePackageUi ui)
        {
            return new MarketplacePackageDownloadService(client, downloader, installer, ui);
        }

        private static MarketplacePackageRequest CreateRequest(string pluginId, string version, string? expectedHash = null)
        {
            return new MarketplacePackageRequest
            {
                PluginId = pluginId,
                Version = version,
                ExpectedHash = expectedHash,
            };
        }

        private sealed class FakeMarketplacePackageClient : IMarketplacePackageClient
        {
            public Func<string, string, string, string?, string?>? ExistingFileResolver { get; set; }
            public bool VerifyFileHashResult { get; set; } = true;
            public int VerifyFileHashCalls { get; private set; }

            public Task<string?> GetLatestVersionAsync(string pluginId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<string?>(null);
            }

            public Task<MarketplacePluginDetail?> GetPluginDetailAsync(string pluginId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<MarketplacePluginDetail?>(null);
            }

            public string GetDownloadUrl(string pluginId, string version)
            {
                return $"https://example.test/{pluginId}/{version}";
            }

            public bool VerifyFileHash(string filePath, string? expectedHash)
            {
                VerifyFileHashCalls++;
                return VerifyFileHashResult;
            }

            public string? GetExistingFileIfValid(string downloadDirectory, string pluginId, string version, string? expectedHash)
            {
                return ExistingFileResolver?.Invoke(downloadDirectory, pluginId, version, expectedHash);
            }
        }

        private sealed class FakeMarketplacePackageDownloader : IMarketplacePackageDownloader
        {
            public List<DownloadInvocation> Invocations { get; } = new();
            public List<DownloadTask> CancelledTasks { get; } = new();
            public Func<DownloadInvocation, DownloadTask>? DownloadFactory { get; set; }
            public bool CompleteImmediately { get; set; } = true;

            public DownloadTask AddDownload(string url, string downloadDirectory, string? authorization, Action<DownloadTask> onCompleted, string fileName)
            {
                var invocation = new DownloadInvocation(url, downloadDirectory, authorization, fileName);
                Invocations.Add(invocation);

                DownloadTask task = DownloadFactory?.Invoke(invocation) ?? new DownloadTask
                {
                    Status = DownloadStatus.Completed,
                    SavePath = Path.Combine(downloadDirectory, fileName),
                };

                if (CompleteImmediately)
                {
                    onCompleted(task);
                }

                return task;
            }

            public void CancelDownload(DownloadTask task)
            {
                CancelledTasks.Add(task);
            }
        }

        private sealed class FakeMarketplacePackageInstaller : IMarketplacePackageInstaller
        {
            public List<InstallInvocation> InstallCalls { get; } = new();

            public void Install(string? restartArguments, params string[] packagePaths)
            {
                InstallCalls.Add(new InstallInvocation(restartArguments, packagePaths));
            }
        }

        private sealed class FakeMarketplacePackageUi : IMarketplacePackageUi
        {
            public FakeMarketplacePackageUi(string downloadDirectory)
            {
                DownloadDirectory = downloadDirectory;
            }

            public string DownloadDirectory { get; }
            public string? Authorization { get; set; } = "user:password";
            public int ShowDownloadWindowCalls { get; private set; }
            public List<string> Warnings { get; } = new();
            public List<string> Errors { get; } = new();
            public List<string?> OpenedFolders { get; } = new();

            public void ShowDownloadWindow()
            {
                ShowDownloadWindowCalls++;
            }

            public void ShowWarning(string message, string title)
            {
                Warnings.Add(message);
            }

            public void ShowError(string message, string title)
            {
                Errors.Add(message);
            }

            public void OpenFolder(string? folderPath)
            {
                OpenedFolders.Add(folderPath);
            }
        }

        private sealed record DownloadInvocation(string Url, string DownloadDirectory, string? Authorization, string FileName);
        private sealed record InstallInvocation(string? RestartArguments, IReadOnlyList<string> PackagePaths);
    }
}
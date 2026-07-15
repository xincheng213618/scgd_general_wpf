#pragma warning disable CA1707
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Marketplace;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
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
        public async Task EnsurePackageAvailableAsync_DeletesInvalidPreferredPackageBeforeDownloading()
        {
            string stalePath = Path.Combine(_downloadDirectory, "PluginA-1.0.0.cvxp");
            Directory.CreateDirectory(_downloadDirectory);
            File.WriteAllBytes(stalePath, new byte[] { 1, 2, 3 });

            int verifyCalls = 0;
            var client = new FakeMarketplacePackageClient
            {
                VerifyFileHashResolver = (_, _) => ++verifyCalls > 1,
            };
            var downloader = new FakeMarketplacePackageDownloader
            {
                DownloadFactory = invocation =>
                {
                    Assert.False(File.Exists(stalePath));
                    return new DownloadTask
                    {
                        Status = DownloadStatus.Completed,
                        SavePath = Path.Combine(invocation.DownloadDirectory, invocation.FileName),
                    };
                },
            };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            string? result = await service.EnsurePackageAvailableAsync(CreateRequest("PluginA", "1.0.0", "expected-hash"), showFailureDialog: false);

            Assert.Equal(stalePath, result);
            Assert.Single(downloader.Invocations);
            Assert.Equal(2, client.VerifyFileHashCalls);
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
        public async Task InstallPackageAsync_InstallsOrdinaryPackageWithoutAdditionalReview()
        {
            string packagePath = CreatePackage("PluginA", "1.0.0", """
                {
                  "id": "PluginA",
                  "name": "Plugin A",
                  "version": "1.0.0"
                }
                """);
            var client = new FakeMarketplacePackageClient { ExistingFileResolver = (_, _, _, _) => packagePath };
            var downloader = new FakeMarketplacePackageDownloader();
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            bool installed = await service.InstallPackageAsync(CreateRequest("PluginA", "1.0.0"));

            Assert.True(installed);
            Assert.Single(installer.InstallCalls);
            Assert.Empty(ui.Confirmations);
            Assert.Empty(ui.Errors);
        }

        [Fact]
        public async Task InstallPackageAsync_ReviewsDeclaredCopilotPermissionsAndBudgets()
        {
            string packagePath = CreateCopilotPackage();
            var client = new FakeMarketplacePackageClient { ExistingFileResolver = (_, _, _, _) => packagePath };
            var downloader = new FakeMarketplacePackageDownloader();
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory) { ConfirmInstallResult = true };
            var service = CreateService(client, downloader, installer, ui);

            bool installed = await service.InstallPackageAsync(CreateRequest("sample.plugin", "2.4.1"));

            Assert.True(installed);
            Assert.Single(installer.InstallCalls);
            string confirmation = Assert.Single(ui.Confirmations);
            Assert.Contains("DelegatePluginReviewer", confirmation, StringComparison.Ordinal);
            Assert.Contains("GrepText", confirmation, StringComparison.Ordinal);
            Assert.Contains("ReadLocalFile", confirmation, StringComparison.Ordinal);
            Assert.Contains("5", confirmation, StringComparison.Ordinal);
            Assert.Contains("8,000", confirmation, StringComparison.Ordinal);
        }

        [Fact]
        public async Task InstallPackageAsync_DoesNotInstallWhenCopilotPermissionReviewIsDeclined()
        {
            string packagePath = CreateCopilotPackage();
            var client = new FakeMarketplacePackageClient { ExistingFileResolver = (_, _, _, _) => packagePath };
            var downloader = new FakeMarketplacePackageDownloader();
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory) { ConfirmInstallResult = false };
            var service = CreateService(client, downloader, installer, ui);

            bool installed = await service.InstallPackageAsync(CreateRequest("sample.plugin", "2.4.1"));

            Assert.False(installed);
            Assert.Empty(installer.InstallCalls);
            Assert.Single(ui.Confirmations);
            Assert.Empty(ui.Errors);
        }

        [Fact]
        public async Task InstallPackageAsync_BlocksInvalidCopilotManifestBeforeLoadingPluginCode()
        {
            string packagePath = CreatePackage("sample.plugin", "2.4.1", """
                {
                  "id": "sample.plugin",
                  "name": "Sample Plugin",
                  "version": "2.4.1",
                  "copilot_agents": [
                    {
                      "id": "plugin-reviewer",
                      "name": "Plugin Reviewer",
                      "description": "Review plugin source.",
                      "instructions": "Read only the requested evidence.",
                      "scope": "WorkspaceReadOnly",
                      "capabilities": ["ExecuteProcess"]
                    }
                  ]
                }
                """);
            var client = new FakeMarketplacePackageClient { ExistingFileResolver = (_, _, _, _) => packagePath };
            var downloader = new FakeMarketplacePackageDownloader();
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, downloader, installer, ui);

            bool installed = await service.InstallPackageAsync(CreateRequest("sample.plugin", "2.4.1"));

            Assert.False(installed);
            Assert.Empty(installer.InstallCalls);
            Assert.Empty(ui.Confirmations);
            Assert.Contains("ExecuteProcess", Assert.Single(ui.Errors), StringComparison.Ordinal);
        }

        [Fact]
        public async Task InstallPackageAsync_BlocksManifestWhoseIdentityDoesNotMatchRequest()
        {
            string packagePath = CreatePackage("PluginA", "1.0.0", """
                {
                  "id": "DifferentPlugin",
                  "name": "Different Plugin",
                  "version": "1.0.0"
                }
                """);
            var client = new FakeMarketplacePackageClient { ExistingFileResolver = (_, _, _, _) => packagePath };
            var installer = new FakeMarketplacePackageInstaller();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(client, new FakeMarketplacePackageDownloader(), installer, ui);

            bool installed = await service.InstallPackageAsync(CreateRequest("PluginA", "1.0.0"));

            Assert.False(installed);
            Assert.Empty(installer.InstallCalls);
            Assert.Contains("DifferentPlugin", Assert.Single(ui.Errors), StringComparison.Ordinal);
        }

        [Fact]
        public void TryApproveBatchInstall_StopsPackagesThatRequireIndividualCopilotReview()
        {
            string packagePath = CreateCopilotPackage();
            var ui = new FakeMarketplacePackageUi(_downloadDirectory);
            var service = CreateService(new FakeMarketplacePackageClient(), new FakeMarketplacePackageDownloader(), new FakeMarketplacePackageInstaller(), ui);

            bool approved = service.TryApproveBatchInstall([packagePath]);

            Assert.False(approved);
            Assert.Single(ui.Warnings);
            Assert.Empty(ui.Confirmations);
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

        private string CreateCopilotPackage()
        {
            return CreatePackage("sample.plugin", "2.4.1", """
                {
                  "id": "sample.plugin",
                  "name": "Sample Plugin",
                  "version": "2.4.1",
                  "copilot_agents": [
                    {
                      "id": "plugin-reviewer",
                      "name": "Plugin Reviewer",
                      "description": "Delegate a bounded plugin code review.",
                      "instructions": "Review only the requested plugin source evidence.",
                      "scope": "WorkspaceReadOnly",
                      "capabilities": ["GrepText", "ReadLocalFile"],
                      "child_mode": "Code",
                      "parent_modes": ["Code", "Diagnose"],
                      "maximum_tool_calls": 5,
                      "maximum_agent_passes": 2,
                      "maximum_duration_seconds": 60,
                      "maximum_answer_characters": 8000
                    }
                  ]
                }
                """);
        }

        private string CreatePackage(string pluginId, string version, string manifestJson)
        {
            Directory.CreateDirectory(_downloadDirectory);
            string packagePath = Path.Combine(_downloadDirectory, $"{pluginId}-{version}.cvxp");
            using ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
            ZipArchiveEntry entry = archive.CreateEntry($"{pluginId}/manifest.json");
            using Stream stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(manifestJson);
            return packagePath;
        }

        private sealed class FakeMarketplacePackageClient : IMarketplacePackageClient
        {
            public Func<string, string, string, string?, string?>? ExistingFileResolver { get; set; }
            public Func<string, string?, bool>? VerifyFileHashResolver { get; set; }
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
                if (VerifyFileHashResolver != null)
                    return VerifyFileHashResolver(filePath, expectedHash);

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
            public List<string> Confirmations { get; } = new();
            public List<string?> OpenedFolders { get; } = new();
            public bool ConfirmInstallResult { get; set; } = true;

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

            public bool ConfirmInstall(string message, string title)
            {
                Confirmations.Add(message);
                return ConfirmInstallResult;
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

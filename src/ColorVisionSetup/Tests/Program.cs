using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVisionSetup
{
    internal static class Program
    {
        private static int _passed;
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception("FAIL: " + name);
            _passed++;
            Console.WriteLine("PASS: " + name);
        }
        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { Check(true, name); return; }
            throw new Exception("FAIL: " + name);
        }
        private static async Task ThrowsAsync<T>(Func<Task> action, string name) where T : Exception
        {
            try { await action(); }
            catch (T) { Check(true, name); return; }
            throw new Exception("FAIL: " + name);
        }
        private static int Main(string[] args)
        {
            try
            {
                RunAsync(args).GetAwaiter().GetResult();
                Console.WriteLine("Passed " + _passed + " checks; no installer was launched.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static async Task RunAsync(string[] args)
        {
            Check(ReleaseClient.ParseLatest("{\"version\":\"1.4.15.34\"}") == new Version(1, 4, 15, 34), "current JSON version contract");
            Check(ReleaseClient.ParseLatest(" 1.4.15.34\n") == new Version(1, 4, 15, 34), "plain version compatibility");
            foreach (string value in new[] { "1.2", "0.0.0.0", "<html>error</html>", "{\"version\":null}", "{\"version\":\"../evil\"}" })
                Throws<InvalidDataException>(() => ReleaseClient.ParseLatest(value), "reject invalid metadata " + value);

            var model = new SetupViewModel();
            Check(!model.CanAct && model.CanCancel, "initial version check blocks competing action but can cancel");
            model.Stage = DownloadStage.Available;
            Check(model.CanAct && !model.CanOpenFolder && model.PrimaryText.Contains("下载"), "one online download action after discovery");
            model.Stage = DownloadStage.Downloading;
            Check(!model.CanAct && !model.CanRefresh, "in-flight download blocks competing work");
            model.IsIndeterminate = true;
            Check(model.ProgressText == "下载中", "unknown download length does not show a fake percentage");
            model.Stage = DownloadStage.Ready;
            Check(model.CanAct && model.CanOpenFolder && model.PrimaryText.Contains("打开"), "verified download offers open action");
            model.Stage = DownloadStage.Opened;
            Check(!model.CanAct && model.CanOpenFolder, "successful handoff blocks repeated launch");

            using (var client = new ReleaseClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"version\":\"1.4.15.34\"}") })))
                Check(await client.GetLatestAsync(CancellationToken.None) == new Version(1, 4, 15, 34), "metadata HTTP response parsed");
            using (var client = new ReleaseClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))))
                await ThrowsAsync<HttpRequestException>(() => client.GetLatestAsync(CancellationToken.None), "failed query cannot create a latest plan");

            string root = Path.Combine(Path.GetTempPath(), "ColorVisionSetup-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes("synthetic download payload");
                string destination = Path.Combine(root, "download.exe");
                using (var client = new ReleaseClient(new StubHandler(request =>
                {
                    Check(request.RequestUri.AbsolutePath == "/api/app/releases/1.4.15.34/download", "uses current complete-package endpoint");
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
                }))) await client.DownloadAsync(new Version(1, 4, 15, 34), destination, null, CancellationToken.None);
                Check(File.ReadAllBytes(destination).SequenceEqual(bytes) && !File.Exists(destination + ".partial"), "only complete transfer is promoted");

                string truncated = Path.Combine(root, "truncated.exe");
                using (var client = new ReleaseClient(new StubHandler(_ =>
                {
                    var content = new ByteArrayContent(bytes);
                    content.Headers.ContentLength = bytes.Length + 9;
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
                }))) await ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(new Version(1, 4, 15, 34), truncated, null, CancellationToken.None), "truncated response rejected");
                Check(!File.Exists(truncated) && !File.Exists(truncated + ".partial"), "failed transfer is not executable and partial removed");

                using (var input = new MemoryStream(bytes))
                using (var output = new MemoryStream())
                {
                    await ReleaseClient.CopyAsync(input, output, null, 1024, null, CancellationToken.None);
                    Check(output.ToArray().SequenceEqual(bytes), "unknown length transfer supported");
                }
                using (var input = new MemoryStream(bytes))
                using (var output = new MemoryStream())
                    await ThrowsAsync<InvalidDataException>(() => ReleaseClient.CopyAsync(input, output, null, 2, null, CancellationToken.None), "oversize stream rejected without trusted length");

                using (var cancellation = new CancellationTokenSource())
                using (var input = new WaitingStream())
                using (var output = new MemoryStream())
                {
                    Task copy = ReleaseClient.CopyAsync(input, output, null, 1024, null, cancellation.Token);
                    await input.Started.Task;
                    cancellation.Cancel();
                    await ThrowsAsync<OperationCanceledException>(() => copy, "cancellation interrupts a non-cooperating pending read");
                }
                Throws<InvalidDataException>(() => InstallerPackage.Verify(destination, null, CancellationToken.None), "unsigned payload cannot become installable");
                Throws<InvalidDataException>(() => PublisherSignature.Verify(typeof(object).Assembly.Location), "non-project publisher rejected");

                string installerArgument = args.FirstOrDefault(x => x != "--online");
                if (installerArgument != null)
                {
                    string real = Path.GetFullPath(installerArgument);
                    var package = InstallerPackage.Verify(real, null, CancellationToken.None);
                    Check(package.Version.Major > 0 && package.Sha256.Length == 64, "real signed ColorVision installer accepted");
                    Throws<InvalidDataException>(() => InstallerPackage.Verify(real, new Version(9, 9, 9, 9), CancellationToken.None), "signed wrong-version package rejected");
                    using (package.OpenVerified())
                        Throws<IOException>(() => { using (File.Open(real, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } }, "handoff verification holds a write/delete exclusion lock");
                    string tampered = Path.Combine(root, "tampered.exe");
                    File.Copy(real, tampered);
                    var verifiedCopy = InstallerPackage.Verify(tampered, package.Version, CancellationToken.None);
                    using (var file = File.Open(tampered, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        file.Position = 0x1000;
                        int value = file.ReadByte();
                        file.Position--;
                        file.WriteByte((byte)(value ^ 0x01));
                    }
                    Throws<InvalidDataException>(() => PublisherSignature.Verify(tampered), "tampered signed executable rejected by WinTrust digest check");
                    Throws<InvalidDataException>(() => { using (verifiedCopy.OpenVerified()) { } }, "changed package rejected before handoff");
                }
                else Console.WriteLine("SKIP: real signed installer checks require one installer path argument.");

                if (args.Contains("--online"))
                {
                    using (var client = new ReleaseClient())
                    {
                        var latest = await client.GetLatestAsync(CancellationToken.None);
                        Check(latest.Major > 0, "live public version endpoint over Framework HttpClient: " + latest);
                    }
                }
            }
            finally
            {
                // This test owns only flat fixture files in its unique temporary directory.
                foreach (string path in Directory.GetFiles(root)) File.Delete(path);
                Directory.Delete(root);
            }
        }

        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            internal StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) { _respond = respond; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(_respond(request));
            }
        }

        private sealed class WaitingStream : Stream
        {
            internal readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<int> _read = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) { Started.TrySetResult(true); return _read.Task; }
            protected override void Dispose(bool disposing) { _read.TrySetException(new ObjectDisposedException(nameof(WaitingStream))); base.Dispose(disposing); }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Flush() { }
        }
    }
}

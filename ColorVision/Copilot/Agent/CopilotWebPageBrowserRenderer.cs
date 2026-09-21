using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    internal sealed record CopilotBrowserResource(int StatusCode, string Headers, byte[] Body);

    // An isolated DOM reader. All browser HTTP traffic uses the same validated transport as FetchUrl.
    internal static class CopilotWebPageBrowserRenderer
    {
        private static readonly SemaphoreSlim Slots = new(1, 1);
        internal const int MaximumResourceBytes = 4 * 1024 * 1024;
        private const int MaximumTotalBytes = 24 * 1024 * 1024;
        private const string ExtractionScript = """
            (() => ({url: location.href, title: document.title.slice(0,512),
              text: (document.querySelector('main') || document.querySelector('article') || document.body)?.innerText?.trim().slice(0,12000) || '',
              description: (document.querySelector('meta[name="description"]')?.content || '').slice(0,1000),
              links: Array.from(document.querySelectorAll('a[href]')).slice(0,100)
                .map(a => ({url:a.href, text:a.innerText.trim().slice(0,160)}))
                .filter(a => a.text && a.url.startsWith(location.origin + '/')).slice(0,12)}))()
            """;

        internal static Task<CopilotFetchedWebPageContent> RenderAsync(Uri uri, CancellationToken token) =>
            RenderAsync(uri, FetchResourceAsync, token);

        internal static async Task<CopilotFetchedWebPageContent> RenderAsync(Uri uri,
            Func<Uri, CancellationToken, Task<CopilotBrowserResource>> fetchResource, CancellationToken token)
        {
            if (!CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri))
                throw new InvalidOperationException("浏览器仅允许读取公开 HTTP/HTTPS 网页。");
            await Slots.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var completion = new TaskCompletionSource<CopilotFetchedWebPageContent>(TaskCreationOptions.RunContinuationsAsynchronously);
                var thread = new Thread(() =>
                {
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                    dispatcher.BeginInvoke(new Action(async () =>
                    {
                        try { completion.TrySetResult(await RenderOnDispatcherAsync(uri, fetchResource, token)); }
                        catch (OperationCanceledException) { completion.TrySetCanceled(token); }
                        catch (Exception ex) { completion.TrySetException(ex); }
                        finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
                    }));
                    Dispatcher.Run();
                }) { IsBackground = true, Name = "Copilot web page reader" };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                return await completion.Task.ConfigureAwait(false);
            }
            finally { Slots.Release(); }
        }

        private static async Task<CopilotFetchedWebPageContent> RenderOnDispatcherAsync(Uri uri,
            Func<Uri, CancellationToken, Task<CopilotBrowserResource>> fetchResource, CancellationToken callerToken)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(25));
            var token = deadline.Token;
            using var host = new HwndSource(new HwndSourceParameters("Copilot web page reader")
            { Width = 1280, Height = 900, WindowStyle = unchecked((int)0x80000000) });
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "Copilot", "BrowserRuntime");
            // The proxy prevents unhandled browser network channels from bypassing the host transport.
            var environment = await CoreWebView2Environment.CreateAsync(null, folder,
                new CoreWebView2EnvironmentOptions("--proxy-server=http://127.0.0.1:9 --proxy-bypass-list=<-loopback> --disable-background-networking"));
            token.ThrowIfCancellationRequested();
            var options = environment.CreateCoreWebView2ControllerOptions();
            options.IsInPrivateModeEnabled = true;
            options.ProfileName = "Read" + Guid.NewGuid().ToString("N");
            var controller = await environment.CreateCoreWebView2ControllerAsync(host.Handle, options);
            var streams = new List<MemoryStream>();
            var pending = new HashSet<Task>();
            var disposed = false;
            var activeRequests = 0;
            var totalRequests = 0;
            var totalBytes = 0;
            var blockedResources = 0;
            var lastActivity = Stopwatch.StartNew();
            var navigated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                token.ThrowIfCancellationRequested();
                controller.Bounds = new System.Drawing.Rectangle(0, 0, 1280, 900);
                var core = controller.CoreWebView2;
                core.Settings.AreHostObjectsAllowed = false;
                core.Settings.IsWebMessageEnabled = false;
                core.Settings.AreDefaultScriptDialogsEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.PermissionRequested += (_, e) => { e.State = CoreWebView2PermissionState.Deny; e.Handled = true; };
                core.DownloadStarting += (_, e) => e.Cancel = true;
                core.NewWindowRequested += (_, e) => e.Handled = true;
                core.LaunchingExternalUriScheme += (_, e) => e.Cancel = true;
                core.NavigationStarting += (_, e) =>
                {
                    if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) || !CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(target)
                        || (uri.Scheme == "https" && target.Scheme != "https"))
                    {
                        e.Cancel = true;
                        navigated.TrySetException(new InvalidOperationException("浏览器导航目标不符合网页读取范围。"));
                    }
                };
                core.FrameNavigationStarting += (_, e) => e.Cancel = true;
                core.NavigationCompleted += (_, e) =>
                {
                    if (e.IsSuccess) navigated.TrySetResult();
                    else navigated.TrySetException(new InvalidOperationException($"浏览器加载网页失败：{e.WebErrorStatus}。"));
                };
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
                core.WebResourceRequested += (_, e) =>
                {
                    var deferral = e.GetDeferral();
                    var operation = RespondAsync(e, deferral);
                    pending.Add(operation);
                };

                async Task RespondAsync(CoreWebView2WebResourceRequestedEventArgs args, CoreWebView2Deferral deferral)
                {
                    activeRequests++;
                    lastActivity.Restart();
                    try
                    {
                        if (++totalRequests > 96 || args.Request.Method != "GET"
                            || !Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var target)
                            || !CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(target))
                            throw new InvalidOperationException("浏览器请求被网页读取范围限制。");
                        var resource = await fetchResource(target, token);
                        if (args.ResourceContext == CoreWebView2WebResourceContext.Document && resource.StatusCode >= 400)
                            throw new HttpRequestException($"网页返回 HTTP {resource.StatusCode}。");
                        totalBytes += resource.Body.Length;
                        if (resource.Body.Length > MaximumResourceBytes || totalBytes > MaximumTotalBytes)
                            throw new InvalidOperationException("浏览器页面资源超过读取大小限制。");
                        if (!disposed)
                        {
                            var stream = new MemoryStream(resource.Body, writable: false);
                            streams.Add(stream);
                            args.Response = environment.CreateWebResourceResponse(stream, resource.StatusCode, "Response", resource.Headers
                                + "\r\nContent-Security-Policy: worker-src 'none'; frame-src 'none'; object-src 'none'; form-action 'none'; connect-src http: https:;\r\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        blockedResources++;
                        if (!disposed)
                        {
                            args.Response = environment.CreateWebResourceResponse(null, 403, "Blocked", "Content-Type: text/plain\r\n");
                            if (args.ResourceContext == CoreWebView2WebResourceContext.Document)
                                navigated.TrySetException(new InvalidOperationException(CopilotUserFacingErrorFormatter.Sanitize(ex.Message), ex));
                        }
                    }
                    finally
                    {
                        activeRequests--;
                        lastActivity.Restart();
                        deferral.Complete();
                    }
                }

                core.Navigate(uri.AbsoluteUri);
                await navigated.Task.WaitAsync(token);
                string previous = string.Empty;
                var stable = Stopwatch.StartNew();
                var observation = Stopwatch.StartNew();
                CopilotFetchedWebPageContent? result = null;
                while (observation.Elapsed < TimeSpan.FromSeconds(12))
                {
                    token.ThrowIfCancellationRequested();
                    var json = await core.ExecuteScriptAsync(ExtractionScript).WaitAsync(token);
                    result = ParseRenderedPage(json);
                    if (!string.Equals(result.Value.Content, previous, StringComparison.Ordinal))
                    { previous = result.Value.Content; stable.Restart(); }
                    if (previous.Length > 0 && observation.Elapsed >= TimeSpan.FromSeconds(2)
                        && stable.Elapsed >= TimeSpan.FromSeconds(1) && activeRequests == 0 && lastActivity.Elapsed >= TimeSpan.FromMilliseconds(500)) break;
                    await Task.Delay(200, token);
                }
                if (result == null || string.IsNullOrWhiteSpace(result.Value.Content))
                    throw new InvalidOperationException("浏览器已加载页面，但没有可读正文；可能需要登录或交互。");
                return result.Value with
                {
                    RenderingNotice = blockedResources > 0 ? $"{blockedResources} resource requests failed or were blocked; rendered content may be incomplete." : null,
                };
            }
            finally
            {
                disposed = true;
                deadline.Cancel();
                controller.CoreWebView2.Stop();
                await Task.WhenAll(pending);
                controller.Close();
                foreach (var stream in streams) stream.Dispose();
            }
        }

        internal static CopilotFetchedWebPageContent ParseRenderedPage(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var uri = new Uri(root.GetProperty("url").GetString()!);
            if (!CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri))
                throw new InvalidOperationException("浏览器返回了范围外的网页地址。");
            var text = root.GetProperty("text").GetString() ?? string.Empty;
            var links = root.GetProperty("links").EnumerateArray()
                .Select(link => new CopilotWebPageLink(link.GetProperty("url").GetString() ?? string.Empty, link.GetProperty("text").GetString() ?? string.Empty))
                .Where(link => Uri.TryCreate(link.Url, UriKind.Absolute, out var target)
                    && CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(target) && target.GetLeftPart(UriPartial.Authority) == uri.GetLeftPart(UriPartial.Authority))
                .Take(CopilotWebPageToolSupport.MaxDiscoveredPageLinks).ToArray();
            return new CopilotFetchedWebPageContent(uri.AbsoluteUri, root.GetProperty("title").GetString() ?? uri.Host,
                root.GetProperty("description").GetString() ?? string.Empty,
                text[..Math.Min(text.Length, CopilotWebPageToolSupport.MaxWebPageContentChars)], RelatedPageLinks: links, BrowserRendered: true);
        }

        internal static async Task<CopilotBrowserResource> FetchResourceAsync(Uri uri, CancellationToken token)
        {
            if (!CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(uri))
                throw new InvalidOperationException("浏览器资源地址不符合网页读取范围。");
            using var client = new HttpClient(CopilotWebPageToolSupport.CreateHttpHandler()) { Timeout = Timeout.InfiniteTimeSpan };
            using var request = CopilotWebPageToolSupport.CreateWebPageRequestMessage(uri);
            request.Headers.UserAgent.ParseAdd("ColorVision-Copilot-Agent/1.0");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.Content.Headers.ContentLength > MaximumResourceBytes)
                throw new InvalidOperationException("浏览器资源超过读取大小限制。");
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[16384];
            int read;
            while ((read = await input.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                if (output.Length + read > MaximumResourceBytes) throw new InvalidOperationException("浏览器资源超过读取大小限制。");
                output.Write(buffer, 0, read);
            }
            var headers = response.Headers.Concat(response.Content.Headers)
                .Where(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Location", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Content-Security-Policy", StringComparison.OrdinalIgnoreCase)
                    || header.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase))
                .Select(header => header.Key + ": " + string.Join(", ", header.Value));
            return new CopilotBrowserResource((int)response.StatusCode, string.Join("\r\n", headers), output.ToArray());
        }
    }
}

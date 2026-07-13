using System.IO.Pipes;
using Newtonsoft.Json;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ColorVisionServiceHost;

internal sealed class ServiceHostPipeServer
{
    private readonly ServiceHostCommandHandler _handler;

    public ServiceHostPipeServer(ServiceHostCommandHandler handler)
    {
        _handler = handler;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ServiceHostLog.Write($"Pipe server listening: {ServiceHostConstants.PipeName}");

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream pipe = NamedPipeServerStreamAcl.Create(
                ServiceHostConstants.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0,
                CreatePipeSecurity());

            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                ServiceHostLog.Write("Pipe client connected.");
                _ = Task.Run(() => HandleClient(pipe), CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Pipe listener failed: {ex}");
                await pipe.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        PipeSecurity security = new();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.InteractiveSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        return security;
    }

    private void HandleClient(NamedPipeServerStream pipe)
    {
        using (pipe)
        {
            using StreamReader reader = new(pipe, ServiceHostJson.Encoding, false, leaveOpen: true);
            using StreamWriter writer = new(pipe, ServiceHostJson.Encoding, leaveOpen: true) { AutoFlush = true };

            try
            {
                ServiceHostLog.Write("Reading pipe request.");
                if (!ServiceHostCallerIdentity.TryResolve(pipe, out ServiceHostRequestContext context, out string identityError))
                {
                    ServiceHostLog.Write($"Pipe caller rejected: {identityError}");
                    writer.WriteLine(JsonConvert.SerializeObject(
                        ServiceHostResponse.FromObject(string.Empty, false, "untrusted_pipe_client"), ServiceHostJson.Settings));
                    return;
                }
                string? requestJson = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(requestJson))
                    return;

                ServiceHostRequest? request = JsonConvert.DeserializeObject<ServiceHostRequest>(requestJson, ServiceHostJson.Settings);
                ServiceHostResponse response = request == null || string.IsNullOrWhiteSpace(request.Command)
                    ? ServiceHostResponse.FromObject(string.Empty, false, "Invalid request.")
                    : _handler.Handle(request, context);

                string responseJson = JsonConvert.SerializeObject(response, ServiceHostJson.Settings);
                writer.WriteLine(responseJson);
            }
            catch (Exception ex)
            {
                ServiceHostLog.Write($"Pipe client failed: {ex}");
            }
        }
    }
}

using ColorVision.SocketProtocol;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class SocketResponseDeliveryTests
{
    [Fact]
    public void ResponseCallbackRunsAfterWritingAndStaysOutOfJson()
    {
        using var stream = new MemoryStream();
        var writesObservedByCallback = new List<string>();
        var response = new SocketResponse
        {
            EventName = "SwitchPG",
            AfterSend = () => writesObservedByCallback.Add(Encoding.UTF8.GetString(stream.ToArray())),
        };
        string json = JsonConvert.SerializeObject(response);

        SocketManager.WriteJsonResponse(stream, response, json);

        Assert.DoesNotContain(nameof(SocketResponse.AfterSend), json);
        Assert.Equal(json, Assert.Single(writesObservedByCallback));
    }

    [Fact]
    public void FailedWriteDoesNotPublishResponse()
    {
        var stream = new MemoryStream();
        stream.Dispose();
        int callbacks = 0;
        var response = new SocketResponse { AfterSend = () => callbacks++ };

        Assert.Throws<ObjectDisposedException>(() => SocketManager.WriteJsonResponse(stream, response, "{}"));

        Assert.Equal(0, callbacks);
    }

    [Fact]
    public void CallbackFailureCannotTurnACompletedWriteIntoATransportError()
    {
        using var stream = new MemoryStream();
        var response = new SocketResponse { AfterSend = () => throw new InvalidOperationException("Callback failed") };

        SocketManager.WriteJsonResponse(stream, response, "{}");

        Assert.Equal("{}", Encoding.UTF8.GetString(stream.ToArray()));
    }
}

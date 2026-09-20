using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProjectARVRPro.SemiAuto.GECS
{
    public sealed class GecsCommandResult
    {
        public bool IsSuccess { get; set; }

        public string ResponseText { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;
    }

    public sealed class GecsClient : IDisposable
    {
        private readonly SemaphoreSlim _commandGate = new SemaphoreSlim(1, 1);
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private GecsFrameReader _reader;
        private string _connectedHost;
        private int _connectedPort;

        public event Action<string> Log;

        public bool IsConnected
        {
            get { return _tcpClient != null && _tcpClient.Connected && _stream != null; }
        }

        public bool IsConnectedTo(string host, int port)
        {
            return IsConnected &&
                   string.Equals(_connectedHost, host, StringComparison.OrdinalIgnoreCase) &&
                   _connectedPort == port;
        }

        public async Task ConnectAsync(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("PG Host 不能为空。", nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "PG Port 必须在 1 到 65535 之间。 ");

            Disconnect();
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(host, port);
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _reader = new GecsFrameReader(_stream);
            _connectedHost = host;
            _connectedPort = port;
            WriteLog("Connected " + host + ":" + port.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<GecsCommandResult> SendCommandAsync(string commandText, string successContains, byte networkNumber, TimeSpan timeout)
        {
            await _commandGate.WaitAsync();
            try
            {
                EnsureConnected();
                await WritePacketAsync(commandText, networkNumber);

                while (true)
                {
                    GecsFrame frame = await _reader.ReadFrameAsync(timeout);
                    if (frame == null)
                        throw new EndOfStreamException("PG 在返回命令结果前关闭了连接。 ");

                    string response = frame.MessageText ?? string.Empty;
                    if (string.Equals(response, "ALIVE", StringComparison.OrdinalIgnoreCase))
                        continue;

                    WriteFrameLog("Received", response, frame.NetworkNumber);
                    if (response.IndexOf("processing", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (response.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) || response.IndexOf(",END,NG", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        WriteLog("Result [NG]: " + response);
                        return new GecsCommandResult
                        {
                            IsSuccess = false,
                            ResponseText = response,
                            ErrorMessage = response
                        };
                    }

                    if (!MatchesSuccessResponse(commandText, response, successContains))
                    {
                        WriteLog("Ignored unmatched response; waiting for: " + successContains);
                        continue;
                    }

                    WriteLog("Result [OK]: " + response);
                    return new GecsCommandResult { IsSuccess = true, ResponseText = response };
                }
            }
            catch (Exception ex)
            {
                WriteLog("Command failed: " + ex.Message);
                Disconnect();
                return new GecsCommandResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
            finally
            {
                _commandGate.Release();
            }
        }

        private static bool MatchesSuccessResponse(string commandText, string response, string successContains)
        {
            if (string.IsNullOrWhiteSpace(successContains))
                return true;
            if (response.IndexOf(successContains, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string[] responseFields = SplitFields(response);
            string[] expectedFields = SplitFields(successContains);
            string[] commandFields = SplitFields(commandText);
            if (expectedFields.Length < 2 || commandFields.Length == 0 || commandFields.Length > responseFields.Length)
                return false;

            for (int index = 0; index < commandFields.Length; index++)
            {
                if (!string.Equals(commandFields[index], responseFields[index], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            int expectedIndex = 0;
            for (int responseIndex = 0; responseIndex < responseFields.Length && expectedIndex < expectedFields.Length; responseIndex++)
            {
                if (string.Equals(responseFields[responseIndex], expectedFields[expectedIndex], StringComparison.OrdinalIgnoreCase))
                    expectedIndex++;
            }
            return expectedIndex == expectedFields.Length;
        }

        private static string[] SplitFields(string value)
        {
            var fields = new List<string>();
            foreach (string field in (value ?? string.Empty).Split(','))
            {
                string trimmed = field.Trim();
                if (trimmed.Length > 0)
                    fields.Add(trimmed);
            }
            return fields.ToArray();
        }

        public void LogCommandPreview(string commandText, byte networkNumber)
        {
            WriteFrameLog("Prepared (not sent)", commandText, networkNumber);
        }

        public async Task<bool> TrySendHeartbeatAsync(byte networkNumber)
        {
            if (!_commandGate.Wait(0))
                return false;

            try
            {
                if (!IsConnected)
                    return false;
                await WritePacketAsync("ALIVE", networkNumber, false);
                return true;
            }
            catch (Exception ex)
            {
                WriteLog("Heartbeat failed: " + ex.Message);
                Disconnect();
                return false;
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_stream != null)
                    _stream.Dispose();
                if (_tcpClient != null)
                    _tcpClient.Close();
            }
            catch
            {
            }
            finally
            {
                _reader = null;
                _stream = null;
                _tcpClient = null;
                _connectedHost = null;
                _connectedPort = 0;
            }
        }

        public void Dispose()
        {
            Disconnect();
            _commandGate.Dispose();
        }

        private async Task WritePacketAsync(string messageText, byte networkNumber, bool writeLog = true)
        {
            EnsureConnected();
            byte[] packet = GecsPacketCodec.BuildPacket(messageText, networkNumber);
            await _stream.WriteAsync(packet, 0, packet.Length);
            await _stream.FlushAsync();
            if (writeLog)
                WriteFrameLog("Sent", messageText, networkNumber, packet);
        }

        private void WriteFrameLog(string direction, string messageText, byte networkNumber, byte[] packet = null)
        {
            byte[] frameBytes = packet ?? GecsPacketCodec.BuildPacket(messageText, networkNumber);
            string lengthHex = Encoding.ASCII.GetString(frameBytes, 2, 4);
            int messageLength = frameBytes.Length - 7;
            WriteLog(direction + " [Network=" + networkNumber.ToString(CultureInfo.InvariantCulture) +
                     "/0x" + networkNumber.ToString("X2", CultureInfo.InvariantCulture) +
                     ", Length=" + lengthHex + " (" + messageLength.ToString(CultureInfo.InvariantCulture) +
                     " bytes), Packet=" + frameBytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes]: " + messageText);
            WriteLog(direction + " HEX: " + BitConverter.ToString(frameBytes).Replace("-", " "));
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException("PG 尚未连接。 ");
        }

        private void WriteLog(string message)
        {
            Action<string> handler = Log;
            if (handler != null)
                handler(message);
        }
    }
}

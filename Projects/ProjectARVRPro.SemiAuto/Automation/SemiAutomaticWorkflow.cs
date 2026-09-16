using ProjectARVRPro.SemiAuto.GECS;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ProjectARVRPro.SemiAuto.Automation
{
    public sealed class SemiAutomaticWorkflowResult
    {
        public bool PgSucceeded { get; set; }

        public bool ArvrConfirmed { get; set; }

        public string CommandText { get; set; } = string.Empty;

        public string PgResponseText { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;
    }

    public sealed class SemiAutomaticWorkflow
    {
        private readonly GecsClient _gecsClient;

        public SemiAutomaticWorkflow(GecsClient gecsClient)
        {
            _gecsClient = gecsClient ?? throw new ArgumentNullException(nameof(gecsClient));
        }

        public async Task<GecsCommandResult> SetPowerAsync(IntegrationProfile profile, bool powerOn, string serialNumber)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            profile.Validate();
            if (!_gecsClient.IsConnectedTo(profile.PgHost, profile.PgPort))
                await _gecsClient.ConnectAsync(profile.PgHost, profile.PgPort);

            string channel = int.Parse(profile.Channel, CultureInfo.InvariantCulture).ToString("00", CultureInfo.InvariantCulture);
            string operation = powerOn ? "ON" : "OFF";
            string command = "PG," + channel + ",POWER," + operation;
            if (powerOn && !string.IsNullOrWhiteSpace(serialNumber))
                command += "," + serialNumber.Trim();

            GecsCommandResult result = await _gecsClient.SendCommandAsync(
                command,
                ",END,OK",
                checked((byte)profile.NetworkNumber),
                TimeSpan.FromSeconds(profile.PgResponseTimeoutSeconds));
            string expectedOperation = ",POWER," + operation;
            if (result.IsSuccess && result.ResponseText.IndexOf(expectedOperation, StringComparison.OrdinalIgnoreCase) < 0)
            {
                result.IsSuccess = false;
                result.ErrorMessage = "PG 回包不是预期的 POWER " + operation + " 结果：" + result.ResponseText;
            }

            return result;
        }

        public async Task<SemiAutomaticWorkflowResult> ExecuteAsync(
            IntegrationProfile profile,
            PgActionMapping mapping,
            string arvrTestType,
            string serialNumber,
            bool confirmArvrAfterSuccess,
            Func<Task<bool>> confirmArvrAsync,
            Action pgSucceeded = null)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));
            if (confirmArvrAfterSuccess && confirmArvrAsync == null)
                throw new ArgumentNullException(nameof(confirmArvrAsync));

            profile.Validate();
            string command = profile.ExpandCommand(mapping, arvrTestType, serialNumber);
            var workflowResult = new SemiAutomaticWorkflowResult { CommandText = command };

            try
            {
                if (!_gecsClient.IsConnectedTo(profile.PgHost, profile.PgPort))
                    await _gecsClient.ConnectAsync(profile.PgHost, profile.PgPort);

                GecsCommandResult commandResult = await _gecsClient.SendCommandAsync(
                    command,
                    mapping.SuccessContains,
                    checked((byte)profile.NetworkNumber),
                    TimeSpan.FromSeconds(profile.PgResponseTimeoutSeconds));
                workflowResult.PgResponseText = commandResult.ResponseText;
                if (!commandResult.IsSuccess)
                {
                    workflowResult.ErrorMessage = commandResult.ErrorMessage;
                    return workflowResult;
                }

                workflowResult.PgSucceeded = true;
                if (pgSucceeded != null)
                    pgSucceeded();

                if (confirmArvrAfterSuccess)
                {
                    if (!await confirmArvrAsync())
                    {
                        workflowResult.ErrorMessage = "PG 已成功，但 ARVR 确认未发送。";
                        return workflowResult;
                    }
                    workflowResult.ArvrConfirmed = true;
                }

                return workflowResult;
            }
            catch (Exception ex)
            {
                workflowResult.ErrorMessage = ex.Message;
                return workflowResult;
            }
        }
    }
}

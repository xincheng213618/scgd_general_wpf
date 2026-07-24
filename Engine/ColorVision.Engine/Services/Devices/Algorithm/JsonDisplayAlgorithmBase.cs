using ColorVision.Engine.Messages;
using ColorVision.Engine.Templates.Jsons;
using MQTTMessageLib.FileServer;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public abstract class JsonDisplayAlgorithmBase<TConfiguration> : DisplayAlgorithmBase<TConfiguration>
        where TConfiguration : SingleTemplateDisplayAlgorithmConfig
    {
        protected JsonDisplayAlgorithmBase(TConfiguration configuration)
            : base(configuration)
        {
        }

        public sealed override MsgRecord? Execute()
        {
            if (!TryGetTemplate(Config.Template, out TemplateJsonParam param) ||
                !TryGetImageInput(out string imageFileName, out FileExtType fileExtType))
            {
                return null;
            }

            return SendCommand(param, string.Empty, string.Empty, imageFileName, fileExtType);
        }

        public abstract MsgRecord SendCommand(
            TemplateJsonParam param,
            string deviceCode,
            string deviceType,
            string fileName,
            FileExtType fileExtType);
    }
}

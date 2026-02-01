using DSC.TLink.ITv2.Enumerations;

namespace DSC.TLink.ITv2.Messages
{
    /// <summary>
    /// Base interface for all ITv2 protocol message data types.
    /// Provides type-safe message handling and automatic serialization via MessageFactory.
    /// </summary>
    internal interface IMessageData
    {
        /// <summary>
        /// Serialize this message to bytes for transmission.
        /// Default implementation delegates to MessageFactory.
        /// Override only if custom serialization logic is required.
        /// </summary>
        List<byte> Serialize(byte? appSequence)
        {
            return MessageFactory.SerializeMessage(appSequence, this);
        }

        ITv2Command Command => MessageFactory.GetCommand(this);
        bool IsAppSequence => MessageFactory.IsAppSequence(Command);
    }
}

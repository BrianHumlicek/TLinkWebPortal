using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Serialization;


namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(ITv2Command.Connection_Encapsulated_Command_for_Multiple_Packets)]
    [SimpleAckTransaction]
    public record MultipleMessagePacket : IMessageData
    {
        public IMessageData[] Messages { get; init; } = Array.Empty<IMessageData>();
    }
}

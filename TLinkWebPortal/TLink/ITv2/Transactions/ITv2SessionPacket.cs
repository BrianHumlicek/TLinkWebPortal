using DSC.TLink.ITv2.Messages;
namespace DSC.TLink.ITv2.Transactions
{
    internal record ITv2MessagePacket(byte senderSequence, byte receiverSequence, byte? appSequence, IMessageData messageData);
    internal record SimpleAckPacket : ITv2MessagePacket
    {
        public SimpleAckPacket(byte senderSequence, byte receiverSequence)
            : base(senderSequence, receiverSequence, null, new SimpleAck())
        {
        }
        public static SimpleAckPacket CreateAckFor(ITv2MessagePacket originalPacket)
        {
            return new SimpleAckPacket(
                senderSequence: originalPacket.receiverSequence,
                receiverSequence: originalPacket.senderSequence
            );
        }
    }
}

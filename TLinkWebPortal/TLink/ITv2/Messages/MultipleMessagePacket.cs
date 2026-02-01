using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Connection_Encapsulated_Command_for_Multiple_Packets)]
    [SimpleAckTransaction]
    internal record MultipleMessagePacket : IMessageData
    {
        public IMessageData[] Messages { get; init; } = Array.Empty<IMessageData>();
    }
}

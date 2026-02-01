using DSC.TLink.ITv2.Enumerations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(ITv2Command.ModuleStatus_Command_Request, isAppSequence: true)]
    [SimpleAckTransaction]
    internal record CommandRequestMessage : IMessageData
    {
        public ITv2Command CommandRequest { get; init; }
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }
}

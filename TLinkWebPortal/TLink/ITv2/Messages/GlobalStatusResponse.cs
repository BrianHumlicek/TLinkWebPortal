using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.ModuleStatus_Global_Status)]
    [SimpleAckTransaction]
    internal record GlobalStatusResponse : IMessageData
    {
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }
}

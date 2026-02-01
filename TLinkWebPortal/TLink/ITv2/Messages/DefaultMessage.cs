using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Messages
{
    [SimpleAckTransaction]
    internal record DefaultMessage : IMessageData
    {
        [IgnoreProperty]
        public ITv2Command Command { get; set; } = ITv2Command.Unknown;
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }
}

using DSC.TLink.ITv2.Enumerations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Command_Error)]
    [SimpleAckTransaction]
    internal record CommandError : IMessageData
    {
        public ITv2Command Command { get; init; }
        public ITv2NackCode NackCode { get; init; }
    }
}

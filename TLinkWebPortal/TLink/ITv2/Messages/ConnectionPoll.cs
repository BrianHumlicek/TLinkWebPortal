using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Connection_Poll)]
    [SimpleAckTransaction]
    internal record ConnectionPoll : IMessageData
    {
    }
}

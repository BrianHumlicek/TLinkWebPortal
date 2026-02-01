using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DSC.TLink.ITv2.ITv2Session;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Notification_Chime_Broadcast)]
    [SimpleAckTransaction]
    internal record NotificationChimeBroadcast : IMessageData
    {
        public byte[] Data { get; init; } = Array.Empty<byte>();
    }
}

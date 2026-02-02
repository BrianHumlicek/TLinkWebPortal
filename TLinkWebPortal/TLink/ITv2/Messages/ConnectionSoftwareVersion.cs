using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Messages
{
    [ITv2Command(Enumerations.ITv2Command.Connection_Software_Version)]
    [SimpleAckTransaction]
    internal record ConnectionSoftwareVersion : IMessageData
    {
        public byte MajorVersion { get; init; }
        public byte MinorVersion { get; init; }
        public byte BuildNumber { get; init; }
        public byte ReleaseNumber { get; init; }
        public byte ProtocolMajorVersion { get; init; }
        public byte ProtocolMinorVersion { get; init; }
        public byte ProductID1 { get; init; }
        public byte ProductID2 { get; init; }
        public byte MarketID { get; init; }
        public byte CustomerID { get; init; }
        public byte ApprovalID { get; init; }
    }
}

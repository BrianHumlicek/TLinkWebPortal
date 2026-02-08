using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Transactions
{
    internal class CommandRequestTransaction : Transaction
    {
        public CommandRequestTransaction(ILogger log, Func<ITv2MessagePacket, CancellationToken, Task> sendMessageDelegate, TimeSpan? timeout = null)
            : base(log, sendMessageDelegate, timeout)
        {
        }

        protected override bool CanContinue => throw new NotImplementedException();

        protected override Task ContinueAsync(ITv2MessagePacket message, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task InitializeInboundAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        protected override Task InitializeOutboundAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}

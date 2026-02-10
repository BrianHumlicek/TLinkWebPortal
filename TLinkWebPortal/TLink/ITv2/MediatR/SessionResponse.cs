using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSC.TLink.ITv2.Messages;

namespace DSC.TLink.ITv2.MediatR
{
    /// <summary>
    /// Response from a SessionCommand.
    /// </summary>
    public record SessionResponse
    {
        public bool Success { get; init; }
        public IMessageData? MessageData { get; init; }
        public string? ErrorMessage { get; init; }
    }
}

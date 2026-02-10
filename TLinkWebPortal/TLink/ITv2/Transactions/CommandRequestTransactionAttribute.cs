// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using Microsoft.Extensions.Logging;

namespace DSC.TLink.ITv2.Transactions
{
    /// <summary>
    /// Marks message types that use the CommandRequestTransaction pattern.
    /// 
    /// Messages with this attribute:
    /// - Request specific data from the panel
    /// - Receive a typed response message matching the requested command
    /// - Complete immediately upon receiving the response (no ack needed)
    /// 
    /// Example: CommandRequestMessage requests zone status → receives ZoneStatusMessage
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    internal sealed class CommandRequestTransactionAttribute : Attribute, ICreateTransaction
    {
        public Transaction CreateTransaction(ILogger log, Func<ITv2MessagePacket, CancellationToken, Task> sendMessageDelegate)
        {
            return new CommandRequestTransaction(log, sendMessageDelegate);
        }
    }
}

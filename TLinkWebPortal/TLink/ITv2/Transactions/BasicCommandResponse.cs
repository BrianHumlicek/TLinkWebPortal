// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using DSC.TLink.ITv2.Messages;
using DSC.TLink.Messages;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSC.TLink.ITv2.Transactions
{
	internal class BasicCommandResponse : ITv2Transaction
	{
		IMediator mediator;
		byte transactionSequence;
		TransactionState state;
		public override bool Complete => throw new NotImplementedException();

		public override bool Success => throw new NotImplementedException();

		public override ReceiveMessageResult ProcessReceivedMessage(ITv2Message headerMessage)
		{
			switch (state)
			{
				case TransactionState.WaitingForCommandResponse:
					if (headerMessage.ReceiverSequence != transactionSequence)
					{
						//message out of sequence
					}
					if (!headerMessage.Command.HasValue)
					{
					}
					if (headerMessage.Command.Value == Enumerations.ITv2Command.Command_Error)
					{

					}
					if (headerMessage.Command.Value != Enumerations.ITv2Command.Command_Response)
					{

					}
					var commandResponse = new CommandResponse();
					if (commandResponse.ResponseCode != Enumerations.CommandResponseCode.Success)
					{

					}
					return new ReceiveMessageResult();
				case TransactionState.WaitingForSimpleAck:
					if (headerMessage.SenderSequence != transactionSequence)
					{
						//message out of sequence
					}
					if (headerMessage.Command.HasValue)
					{
						//not a simple ack
					}
					//mediator.Publish();
					return new ReceiveMessageResult();
				default:
					throw new InvalidOperationException($"Unknown transaction state {state}");
			}
			throw new NotImplementedException();
		}

		protected override void processSentMessage(ITv2MessageData messageData)
		{
			throw new NotImplementedException();
		}

		enum TransactionState
		{
			WaitingForSimpleAck,
			WaitingForCommandResponse
		}
	}
}

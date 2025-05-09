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

using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Messages;
using MediatR;

namespace DSC.TLink.ITv2.Transactions
{
	internal abstract class ITv2Transaction
	{
		public Task Completion => tcs.Task;
		TaskCompletionSource tcs = new TaskCompletionSource();
		protected void completeTransaction()
		{
			tcs.SetResult();
		}
		IMediator mediator;
		public Node InitiatingNode { get; }
		public abstract bool Success { get; }
		public abstract bool Complete { get; }
		public abstract ReceiveMessageResult ProcessReceivedMessage(ITv2Message message);
		protected abstract void processSentMessage(ITv2MessageData messageData);
		public record ReceiveMessageResult ()
		{
			public bool Success { get; init; } = true;
			public bool SendSimpleAck { get; init; }
			public ITv2MessageData? Response { get; init; }
			public bool SendResponse => Response != null;
		}

		static readonly Dictionary<ITv2Command, Type> lookup = new Dictionary<ITv2Command, Type>();
		//static ITv2Transaction create(ITv2Command command) => command switch
		//{
		//	ITv2Command.Connection_Open_Session => new ITv2CommandResponseTransaction<OpenSessionMessage>(),
		//	ITv2Command.Connection_Request_Access => new ITv2CommandResponseTransaction<RequestAccess>(),
		//	ITv2Command.Notification_Time_Date_Broadcast => new ITv2CommandResponseTransaction<OpenSessionMessage>(),
		//	_ => throw new NotImplementedException()
		//};
		//public static ITv2Transaction CreateOutboundTransaction<T>(T messageData) where T : ITv2MessageData, new()
		//{
		//	var transaction = create(messageData.Command);
		//	transaction.processSentMessage(messageData);
		//	return transaction;
		//}
		//public static ITv2Transaction CreateInboundTransaction(ITv2Command command, IMediator mediator)//, Span<byte> messageBytes)
		//{
		//	var transaction = create(command);
		//	transaction.mediator = mediator;
		//	return transaction;
		//}
		public enum Node
		{
			Unknown,
			Local,
			Remote
		}
	}
}

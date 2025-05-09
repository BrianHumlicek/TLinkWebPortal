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
using DSC.TLink.ITv2.Transactions;
using DSC.TLink.Messages;

namespace DSC.TLink.ITv2
{
	internal partial class ITv2Session
	{
		public class InitializingTransaction : Transaction
		{
			InitializationState state = InitializationState.ReceiveOpenSession;
			OpenSessionMessage openSessionMessage;
			public InitializingTransaction(ITv2Session session) : base(session)
			{
			}
			public override async Task ProcessReceivedMessageAsync(ITv2Message message)
			{
				try
				{
					await processTransactionState(message);
				}
				catch (Exception ex)
				{
					resetState();
					await session.SendCommandResponse((CommandResponseCode)1);
				}
			}

			void resetState()
			{
				session.encryptionHandler?.Dispose();
				session.encryptionHandler = null;
				state = InitializationState.ReceiveOpenSession;
			}
			async Task processTransactionState(ITv2Message message)
			{
				switch (state)
				{
					case InitializationState.ReceiveOpenSession:
						openSessionMessage = validateMessageType<OpenSessionMessage>(message);
						await session.SendCommandResponse();
						state = InitializationState.ReceiveOpenSessionSimpleAck;
						return;
					case InitializationState.ReceiveOpenSessionSimpleAck:
						validateMessageType<SimpleAck>(message);
						session.encryptionHandler = openSessionMessage.EncryptionType switch
						{
							EncryptionType.Type1 => new ITv2EncryptionType1(session.configuration),
							EncryptionType.Type2 => new ITv2EncryptionType2(session.configuration),
							_ => throw new TLinkPacketException(TLinkPacketException.Code.EncodingError)
						};
						await session.SendMessage(openSessionMessage);
						state = InitializationState.SentOpenSession;
						return;
					case InitializationState.SentOpenSession:
						validateMessageType<CommandResponse>(message);
						session.tl280Sequence++;
						await session.SendSimpleAck();
						state = InitializationState.ReceiveRequestAccess;
						return;
					case InitializationState.ReceiveRequestAccess:
						var requestAccess = validateMessageType<RequestAccess>(message);
						session.encryptionHandler!.ConfigureOutboundEncryption(requestAccess.Initializer);
						await session.SendCommandResponse();
						state = InitializationState.ReceiveRequestAccessSimpleAck;
						return;
					case InitializationState.ReceiveRequestAccessSimpleAck:
						validateMessageType<SimpleAck>(message);
						var initializer = session.encryptionHandler!.ConfigureInboundEncryption();
						session.localSequence++;
						await session.SendMessage(new RequestAccess() { Initializer = initializer });
						state = InitializationState.SentRequestAccess;
						return;
					case InitializationState.SentRequestAccess:
						validateMessageType<CommandResponse>(message);
						session.tl280Sequence++;
						await session.SendSimpleAck();
						state = InitializationState.Complete;
						return;
					default:
						throw new InvalidOperationException($"Invalid state {state}");
				}
			}
			T validateMessageType<T>(ITv2Message message)
			{
				if (message is T tMessage)
				{
					return tMessage;
				}
				throw new TLinkPacketException(TLinkPacketException.Code.UnexpectedResponse);
			}
		}
		enum InitializationState
		{
			ReceiveOpenSession = 0,
			ReceiveOpenSessionSimpleAck = 1,

			SentOpenSession = 2,

			ReceiveRequestAccess = 3,
			ReceiveRequestAccessSimpleAck = 4,

			SentRequestAccess = 5,
			Complete = 6
		}
	}
}

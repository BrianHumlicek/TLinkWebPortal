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

using DSC.TLink.ITv2.Messages;
using Microsoft.Extensions.Logging;

namespace DSC.TLink.ITv2
{
	internal partial class ITv2Session
	{
		/// <summary>
		/// Simple ITv2 transaction pattern with just message + ack.
		/// 
		/// Protocol Flow:
		/// Inbound:
		/// 1. Receive data message from remote
		/// 2. Send SimpleAck to acknowledge
		/// 3. Transaction complete
		/// 
		/// Outbound:
		/// 1. Send data message to remote
		/// 2. Wait for SimpleAck from remote
		/// 3. Transaction complete
		/// 
		/// This is used for broadcasts and notifications that don't need a CommandResponse.
		/// </summary>
		class SimpleAckTransaction : Transaction
		{
			private State _state;

			public SimpleAckTransaction(ITv2Session session, TimeSpan? timeout = null) 
				: base(session, timeout)
			{
				_state = State.Initial;
			}

			protected override bool CanContinue => _state switch
			{
				State.AwaitingAck => true,
				_ => false
			};

			protected override async Task InitializeInboundAsync(CancellationToken cancellationToken)
			{
				// Inbound: Remote sent us a message, send SimpleAck back
				session._log.LogTrace("SimpleAck transaction: received message, sending ack");
				_state = State.SendingSimpleAck;
				await SendMessageAsync(new SimpleAck(), cancellationToken);
				_state = State.Complete;
				session._log.LogDebug("SimpleAck transaction completed (inbound)");
			}

			protected override async Task InitializeOutboundAsync(CancellationToken cancellationToken)
			{
				// Outbound: We sent a message, wait for SimpleAck
				session._log.LogTrace("SimpleAck transaction: sent message, awaiting ack");
				_state = State.AwaitingAck;
				await Task.CompletedTask; // Nothing more to send yet
			}

			protected override async Task ContinueAsync(ITv2Message message, CancellationToken cancellationToken)
			{
				switch (_state)
				{
					case State.AwaitingAck:
						// We sent a message, expecting Ack back
						if (message.messageData is SimpleAck)
						{
                            _state = State.Complete;
                            session._log.LogDebug("SimpleAck transaction completed (outbound)");
                            break;
                            session._log.LogWarning("Expected SimpleAck, got {Type}", message.messageData.GetType().Name);
							Abort();
							return;
						}
                        else if (message.messageData is CommandError errorMessage)
                        {
                            _state = State.Complete;
                            session._log.LogWarning("Received CommandError NAck: Command={Command}, NackCode={NackCode}",
                                errorMessage.Command, errorMessage.NackCode);
                            break;
                        }
                        session._log.LogError("Expected Ack, got {Type}", message.messageData.GetType().Name);
                        Abort();
                        break;
                    default:
						session._log.LogWarning("Unexpected message in state {State}", _state);
						break;
				}

				await Task.CompletedTask;
			}

            ////This is here to handle the case where the panel seems to respond to heartbeat messages with a command message instead of a SimpleAck.
            ////When this happens, I dont expect to getthe SimpleAck later, so we abort the transaction.  It also seems to serve as a ack to get a
            ////message, so, there is no exception to throw.  By returning false, we indicate that this message is not correlated to this transaction.
            ////So a new transaction will kick off in the message pump of the session.
            //protected override bool isCorrelatedMessage(ITv2Message message)
            //{
            //    if (_state == State.AwaitingAck && message.messageData is not SimpleAck)
            //    {
            //        session._log.LogDebug("Expected SimpleAck, got {Type}", message.messageData.GetType().Name);
            //        Abort();
            //        return false;
            //    }
            //    return true;
            //}

			private enum State
			{
				Initial,
				SendingSimpleAck,     // Inbound: sending our ack
				AwaitingAck,    // Outbound: waiting for remote's ack
				Complete
			}
		}
	}
}
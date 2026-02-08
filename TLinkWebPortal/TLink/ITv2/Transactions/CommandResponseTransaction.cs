using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.ITv2.Messages;
using Microsoft.Extensions.Logging;

namespace DSC.TLink.ITv2.Transactions
{
	/// <summary>
	/// Standard ITv2 command-response transaction pattern.
	/// 
	/// Protocol Flow:
	/// 1. Send Command message
	/// 2. Wait for CommandResponse from remote
	/// 3. Send SimpleAck to acknowledge
	/// 4. Transaction complete
	/// 
	/// This is the most common transaction type in ITv2 protocol.
	/// </summary>
	internal class CommandResponseTransaction : Transaction
	{
		private State _state;
		private CommandResponseCode? _responseCode;

		public CommandResponseTransaction(ILogger log, Func<ITv2MessagePacket, CancellationToken, Task> sendMessageDelegate, TimeSpan? timeout = null) 
			: base(log, sendMessageDelegate, timeout)
		{
			_state = State.Initial;
		}

        protected override bool CanContinue => _state switch
        {
            State.AwaitingCommandResponse => true,
            State.AwaitingSimpleAck => true,
            _ => false
        };


        protected override async Task InitializeInboundAsync(CancellationToken cancellationToken)
		{
			// Inbound: Remote sent us a command, send CommandResponse back
			_state = State.SendingCommandResponse;
			await SendMessageAsync(new CommandResponse { ResponseCode = CommandResponseCode.Success }, cancellationToken);
			_state = State.AwaitingSimpleAck;
		}

		protected override async Task InitializeOutboundAsync(CancellationToken cancellationToken)
		{
			// Outbound: We sent a command, wait for CommandResponse
			_state = State.AwaitingCommandResponse;
			await Task.CompletedTask; // Nothing to send yet
		}

		protected override async Task ContinueAsync(ITv2MessagePacket message, CancellationToken cancellationToken)
		{
			switch (_state)
			{
				case State.AwaitingCommandResponse:
					// We sent a command, expecting CommandResponse back
					if (message.messageData is not CommandResponse response)
					{
						log.LogWarning("Expected CommandResponse, got {Type}", message.messageData.GetType().Name);
						Abort();
						return;
					}

					_responseCode = response.ResponseCode;
						
					if (response.ResponseCode != CommandResponseCode.Success)
					{
						log.LogWarning("Command rejected with code {Code}", response.ResponseCode);
						// Still complete the protocol by sending ack
					}

					// Send SimpleAck to complete transaction
					_state = State.SendingSimpleAck;
					await SendMessageAsync(new SimpleAck(), cancellationToken);
					_state = State.Complete;
					log.LogDebug("CommandResponse transaction completed with code {Code}", _responseCode);
					break;

				case State.AwaitingSimpleAck:
					// We sent CommandResponse, expecting SimpleAck back
					if (message.messageData is not SimpleAck)
					{
						log.LogWarning("Expected SimpleAck, got {Type}", message.messageData.GetType().Name);
						Abort();
						return;
					}

					_state = State.Complete;
					log.LogDebug("CommandResponse transaction completed");
					break;

				default:
					log.LogWarning("Unexpected message in state {State}", _state);
					break;
			}
		}

		/// <summary>
		/// Get the response code if the transaction completed (for outbound transactions).
		/// </summary>
		public CommandResponseCode? ResponseCode => _responseCode;

		private enum State
		{
			Initial,
			SendingCommandResponse,      // Inbound: sending our response
			AwaitingSimpleAck,           // Inbound: waiting for remote's ack
			AwaitingCommandResponse,     // Outbound: waiting for remote's response
			SendingSimpleAck,            // Outbound: sending our ack
			Complete
		}
	}
}

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

using DSC.TLink.Extensions;
using DSC.TLink.ITv2.Messages;
using DSC.TLink.ITv2.Transactions;
using Microsoft.Extensions.Logging;
using MediatR;
using Microsoft.Extensions.Configuration;
using System.IO.Pipelines;
using DSC.TLink.ITv2.Enumerations;

namespace DSC.TLink.ITv2
{
	internal partial class ITv2Session : IDisposable
	{
		ILoggerFactory loggerFactory;
		ILogger log;
		TLinkClient tlinkClient;
		byte localSequence;	//This is the sequence number that this server is updating and the TL280 is checking
		byte tl280Sequence;	//This is the sequence number that the TL280 is updating amd this server is checking.
		byte appSequence;
		ITv2Encryption? encryptionHandler;
		Transaction transactionContext;
		IMediator mediator;
		IConfiguration configuration;
		public ITv2Session(IMediator mediator, ILoggerFactory loggerFactory)
		{
			this.loggerFactory = loggerFactory;
			this.log = loggerFactory.CreateLogger<ITv2Session>();
			this.mediator = mediator;
			this.transactionContext = new InitializingTransaction(this);
		}
		bool transactionIsActive(Transaction context) => !context.Completion.IsCompleted;
		bool transactionIsActive() => transactionIsActive(transactionContext);

		public async Task ListenAsync(IDuplexPipe transport, CancellationToken cancellationToken = default)
		{
			tlinkClient = new TLinkClient(transport, loggerFactory.CreateLogger<TLinkClient>());
			while (!cancellationToken.IsCancellationRequested || transactionIsActive())
			{
				ITv2Message message = await readMessageAsync();

				validateLocalSequence(message);

				if (transactionIsActive())
				{
					validateRemoteSequence(message);
					await transactionContext!.ProcessReceivedMessageAsync(message);
				}
				else if (message.Command.HasValue)
				{
					await startTransactionAsync(message);
				}
				else
				{
					//simple ack handler
				}
			}
		}
		async Task startTransactionAsync(ITv2Message message)
		{
			var transaction = TransactionFactory.Create(message.Command!.Value, this);

			await setActiveTransactionAsync(transaction);

			tl280Sequence = message.SenderSequence;

			await transactionContext!.ProcessReceivedMessageAsync(message);
		}

		/// <summary>
		/// Threadsafe method to set the session transactionContext field.
		/// </summary>
		/// <param name="newTransaction"></param>
		/// <returns></returns>
		async Task setActiveTransactionAsync(Transaction newTransaction)
		{
			var currentTransaction = transactionContext;
			while (transactionIsActive(currentTransaction) ||
				   Interlocked.CompareExchange(ref transactionContext, newTransaction, currentTransaction) != currentTransaction)
			{
				await transactionContext!.Completion;
				currentTransaction = transactionContext;
			}
		}
		void validateLocalSequence(ITv2Message header)
		{
			if (header.ReceiverSequence != localSequence)
			{
				//local sequence error
			}
		}
		void validateRemoteSequence(ITv2Message header)
		{
			if (header.SenderSequence != tl280Sequence)
			{
				//remote sequence error
			}
		}
		async Task<ITv2Message> readMessageAsync(CancellationToken cancellationToken = default)
		{
			var readResult = await tlinkClient.ReadMessage(cancellationToken);

			if (readResult.IsComplete) throw new TLinkPacketException(TLinkPacketException.Code.Disconnected);

			byte[] messageBytes = readResult.Payload;

			messageBytes = encryptionHandler?.HandleInboundData(messageBytes) ?? messageBytes;

			log.LogDebug("Received {message}", messageBytes);

			return new ITv2Message(messageBytes);
		}
		public async Task SendSimpleAck()
		{
			var simpleAck = new SimpleAck()
			{
				HostSequence = localSequence,
				RemoteSequence = tl280Sequence
			};
			await sendMessageBytes(simpleAck.ToByteArray());
		}
		public async Task SendCommandResponse(CommandResponseCode responseCode = CommandResponseCode.Success)
		{
			await SendMessage(new CommandResponse() { ResponseCode = responseCode });
		}
		public async Task SendMessage(ITv2MessageData message)
		{
			var header = new ITv2Message()
			{
				SenderSequence = localSequence,
				ReceiverSequence = tl280Sequence,
				Command = message.Command,
				CommandData = message.ToByteArray()
			};

			await sendMessageBytes(header.ToByteArray());
		}
		async Task sendMessageBytes(byte[] messageBytes)
		{
			log.LogDebug("Sent     {message}", messageBytes);

			messageBytes = encryptionHandler?.HandleOutboundData(messageBytes) ?? messageBytes;

			await tlinkClient.SendMessage(messageBytes);
		}
		public void Dispose()
		{
			encryptionHandler?.Dispose();
		}
	}
}

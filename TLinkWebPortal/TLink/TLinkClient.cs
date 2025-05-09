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

using Microsoft.Extensions.Logging;
using DSC.TLink.Extensions;
using System.IO.Pipelines;
using System.Buffers;

namespace DSC.TLink
{
	public class TLinkClient
	{
		IDuplexPipe transport;
		protected ILogger log;

		public TLinkClient(IDuplexPipe transport, ILogger<TLinkClient> log)
		{
			this.transport = transport;
			this.log = log;
		}

		byte[]? _defaultHeader;
		public byte[] DefaultHeader
		{
			get => _defaultHeader ??= Array.Empty<byte>();
			set => _defaultHeader = value ?? throw new ArgumentNullException(nameof(DefaultHeader));
		}

		//3060 Local Port [851][105] Ethernet receiver 1
		//3061 Remote Port [851][104] Ethernet receiver 1
		//3062 DLS Incomming port [851][012]
		//3066 DLS Outgoing port [851][013]
		//3072 [851][429] Integraion notification Integration session 1
		//3073 [851][430] Integration polling Integration session 1
		//3070 [851][432] Integration Outgoing Integration session 1
		//3071 [851][433] Integration Incoming Integration session 1
		//3092 SA Incomming port [851][095]
		//3094 SA Outgoing port.[851][096]  messages sent after an SMS request
		//3064 is the "ReceiverPort"  Not sure where this came from

		#region Sending logic
		public async Task SendMessage(byte[] payload, CancellationToken cancellationToken = default) => await SendMessage(DefaultHeader, payload, cancellationToken);
		public async Task SendMessage(byte[] header, byte[] payload, CancellationToken cancellationToken = default)
		{
			log?.LogTrace(() => $"Sending header '{Array2HexString(header)}' with message '{Array2HexString(payload)}'");

			var stuffedHeader = stuffBytes(header);
			var stuffedPayload = stuffBytes(payload);

			byte[] packet = stuffedHeader.Concat(0x7E).Concat(stuffedPayload).Concat(0x7F).ToArray();

			log?.LogDebug("Sent     {packet}", packet);

			await sendPacket(packet, cancellationToken);

			IEnumerable<byte> stuffBytes(IEnumerable<byte> inputBytes)
			{
				foreach (byte b in inputBytes)
				{
					switch (b)
					{
						case 0x7D:
							yield return 0x7D;
							yield return 0x00;
							break;
						case 0x7E:
							yield return 0x7D;
							yield return 0x01;
							break;
						case 0x7F:
							yield return 0x7D;
							yield return 0x02;
							break;
						default:
							yield return b;
							break;
					}
				}
			}
		}
		protected virtual async Task sendPacket(byte[] packet, CancellationToken cancellationToken) => await transport.Output.WriteAsync(new ReadOnlyMemory<byte>(packet), cancellationToken);
		#endregion
		#region Receiving logic
		public async Task<TLinkReadResult> ReadMessage(CancellationToken cancellationToken = default)
		{
			(ReadOnlySequence<byte> packetSequence,
			bool isCanceled,
			bool isComplete) = await readPacketBytes(cancellationToken);
			
			(byte[] header, byte[] payload) message;
			try
			{
				message = parseTLinkFrame(packetSequence);
			}
			catch (TLinkPacketException tLinkPackException)
			{
				tLinkPackException.PacketData = Array2HexString(packetSequence.ToArray());
				throw;
			}
			//AdvanceTo invalidates the readonly sequence that was read, so it can only be called after we are done with the sequence.
			transport.Input.AdvanceTo(packetSequence.End);

			if (log?.IsEnabled(LogLevel.Trace) ?? false)
			{
				log?.LogTrace("Received header '{header}'", message.header);
				log?.LogTrace("Received payload '{payload}'", message.payload);
			}

			_defaultHeader ??= message.header;


			return new TLinkReadResult()
			{
				Header = message.header,
				Payload = message.payload,
				IsCanceled = isCanceled,
				IsComplete = isComplete
			};
		}

		async Task<(ReadOnlySequence<byte> packet, bool isCanceled, bool isComplete)> readPacketBytes(CancellationToken cancellationToken)
		{
			//The while loop is here because its possible the first read attempt results in a partial buffer.
			//In this case, tryGetPacket will fail, and the loop will retry and hopefully get more data in
			//the buffer to complete the packet.
			while (true)
			{
				ReadResult readResult = await transport.Input.ReadAtLeastAsync(2, cancellationToken);
				if (readResult.IsCanceled)
				{
					throw new TLinkPacketException(TLinkPacketException.Code.Cancelled) { PacketData = Array2HexString(readResult.Buffer.ToArray()) };
				}
				ReadOnlySequence<byte> packetSlice;
				if (tryGetPacketSlice(readResult.Buffer, out packetSlice))
				{
					return (packetSlice, readResult.IsCanceled, readResult.IsCompleted);
				}
			}
		}

		protected virtual bool tryGetPacketSlice(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> packetSlice)
		{
			SequencePosition? delimiter = buffer.PositionOf((byte)0x7F);
			if (!delimiter.HasValue)
			{
				packetSlice = default;
				return false;
			}

			SequencePosition delimiterInclusivePosition = buffer.GetPosition(1, delimiter.Value);

			packetSlice = buffer.Slice(0, delimiterInclusivePosition);
			if (log.IsEnabled(LogLevel.Debug))
			{
				log.LogDebug("Received {rawPacket}", packetSlice);
			}
			return true;
		}

		protected virtual (byte[] header, byte[] payload) parseTLinkFrame(ReadOnlySequence<byte> packetSequence)
		{
			SequenceReader<byte> packetReader = new SequenceReader<byte>(packetSequence);
			
			ReadOnlySequence<byte> headerSequence;
			if (!packetReader.TryReadTo(out headerSequence, 0x7E, true)) throw new TLinkPacketException(TLinkPacketException.Code.FramingError, "Unable to find header delimiter 0x7E");

			ReadOnlySequence<byte> payloadSequence;
			if (!packetReader.TryReadTo(out payloadSequence, 0x7F, true)) throw new TLinkPacketException(TLinkPacketException.Code.FramingError, "Unable to find header delimiter 0x7F");

			byte[] header = unstuffSequence(headerSequence);
			byte[] payload = unstuffSequence(payloadSequence);

			return (header, payload);
			
			byte[] unstuffSequence(ReadOnlySequence<byte> stuffedSequence)
			{
				//Validate the encoding
				if (stuffedSequence.PositionOf((byte)0x7E).HasValue) throw new TLinkPacketException(TLinkPacketException.Code.EncodingError, "Invalid byte 0x7E in encoded packet");
				if (stuffedSequence.PositionOf((byte)0x7F).HasValue) throw new TLinkPacketException(TLinkPacketException.Code.EncodingError, "Invalid byte 0x7F in encoded packet");

				List<byte>? resultList = default;
				SequenceReader<byte> stuffedSequenceReader = new SequenceReader<byte>(stuffedSequence);
				do
				{
					ReadOnlySequence<byte> unencodedSequence;
					if (!stuffedSequenceReader.TryReadTo(out unencodedSequence, 0x7D, true))	//If we can't find the delimiter that means there are no 'stuffings' in the entire (or remaining) sequence, so time to return
					{
						if (resultList == null)
						{
							//Short circuit return if there are no encoded bytes
							return stuffedSequence.ToArray();
						}
						resultList.AddRange(stuffedSequenceReader.UnreadSequence.ToArray());
						return resultList.ToArray();
					}
					resultList ??= new List<byte>((int)stuffedSequence.Length - 1); //Length - 1 is because here I know there is at least 1 encoded byte, which reduces the unencoded length by 1, so its a reasonable guess to limit allocation.
					resultList.AddRange(unencodedSequence.ToArray());

					byte encodedByte;
					if (!stuffedSequenceReader.TryRead(out encodedByte)) throw new TLinkPacketException(TLinkPacketException.Code.EncodingError, "Unexpected end of packet.");
					switch (encodedByte)
					{
						case 0:
							resultList.Add(0x7D);
							break;
						case 1:
							resultList.Add(0x7E);
							break;
						case 2:
							resultList.Add(0x7F);
							break;
						default:
							throw new TLinkPacketException(TLinkPacketException.Code.EncodingError, $"Unknown encoding value {encodedByte:X2}");
					}

				} while (true);
			}
		}
		#endregion
		string Array2HexString(IEnumerable<byte> inputBytes) => ILoggerExtensions.Enumerable2HexString(inputBytes);

		public struct TLinkReadResult
		{
			public byte[] Header { get; init; }
			public byte[] Payload { get; init; }
			public bool IsCanceled { get; init;  }
			public bool IsComplete { get; init; }

		}
	}
}
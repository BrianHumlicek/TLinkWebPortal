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
using DSC.TLink.Messages;
using DSC.TLink.Messages.Extensions;

namespace DSC.TLink.ITv2.Messages
{
	internal record RequestAccess : AppSequenceMessage
	{
		public byte[] Initializer { get => initializer.Get(); set => initializer.Set(value); }

		public override ITv2Command Command => ITv2Command.Connection_Request_Access;

		readonly IArrayProperty initializer = new LeadingLengthArray();
		protected override List<byte> buildByteList()
		{
			var baseList = base.buildByteList();
			baseList.AddRange(initializer.ToMessageBytes());
			return baseList;
		}

		protected override ReadOnlySpan<byte> initialize(ReadOnlySpan<byte> bytes)
		{
			bytes = base.initialize(bytes);
			bytes.PopAndSetValue(initializer);
			return bytes;
		}
	}
}

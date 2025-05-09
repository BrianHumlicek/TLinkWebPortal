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
using System.ComponentModel;
using static DSC.TLink.TLinkPacketException;

namespace DSC.TLink
{
	public partial class TLinkPacketException
	{
		public enum Code
		{
			Unknown = default,
			[Description("The send/receive operation was cancelled.")]
			Cancelled,
			[Description("The send/receive operation has timed out.")]
			Timeout,
			[Description("The underlying transport has unexpectedly disconnected.")]
			Disconnected,
			[Description("Received data with an invalid length.")]
			InvalidLength,
			[Description("The message CRC check failed.")]
			CRCFailure,
			[Description("The message framing wasn't correct.")]
			FramingError,
			[Description("The data encoding wasn't correct.")]
			EncodingError,
			[Description("The response received wasn't expected.")]
			UnexpectedResponse
		}
	}
	public static class TLinkPacketExceptionCodeExtensions
	{
		public static string Description(this Code exceptionCode) => EnumExtensions.GetMemberDescription(exceptionCode);
	}
}

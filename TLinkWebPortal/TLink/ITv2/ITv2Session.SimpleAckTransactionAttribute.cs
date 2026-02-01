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

using System;

namespace DSC.TLink.ITv2
{
	/// <summary>
	/// Marks a message type as using the SimpleAck transaction pattern.
	/// 
	/// Flow:
	/// 1. Message (data)
	/// 2. SimpleAck
	/// 3. Complete
	/// 
	/// Used for broadcasts, notifications, and one-way messages that need acknowledgment
	/// but don't require a CommandResponse.
	/// </summary>
	/// <example>
	/// [SimpleAckTransaction]
	/// public record DateTimeBroadcast : IMessageData
	/// {
	///     // ... properties
	/// }
	/// </example>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	internal sealed class SimpleAckTransactionAttribute : Attribute
	{
		/// <summary>
		/// Optional timeout for the transaction. If null, uses default timeout.
		/// </summary>
		public TimeSpan? Timeout { get; set; }

		public SimpleAckTransactionAttribute()
		{
		}

		/// <summary>
		/// Create a SimpleAck transaction attribute with a custom timeout.
		/// </summary>
		/// <param name="timeoutSeconds">Timeout in seconds</param>
		public SimpleAckTransactionAttribute(int timeoutSeconds)
		{
			Timeout = TimeSpan.FromSeconds(timeoutSeconds);
		}
	}
}
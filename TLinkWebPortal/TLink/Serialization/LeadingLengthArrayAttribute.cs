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

namespace DSC.TLink.Serialization
{
    /// <summary>
    /// Marks a byte array property as having a variable length with a leading length prefix.
    /// The serialized format is: [Length:1-2 bytes][Data:N bytes]
    /// </summary>
    /// <example>
    /// // With 1-byte length prefix (max 255 bytes)
    /// [LeadingLengthArray]
    /// public byte[] Payload { get; init; }
    /// 
    /// // With 2-byte length prefix (max 65535 bytes)
    /// [LeadingLengthArray(lengthBytes: 2)]
    /// public byte[] LargePayload { get; init; }
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class LeadingLengthArrayAttribute : Attribute
    {
        /// <summary>
        /// The number of bytes used to encode the array length (1 or 2).
        /// </summary>
        public int LengthBytes { get; }

        /// <summary>
        /// Create a variable-length array attribute with length prefix.
        /// </summary>
        /// <param name="lengthBytes">Number of bytes for the length prefix (1 or 2)</param>
        public LeadingLengthArrayAttribute(int lengthBytes = 1)
        {
            if (lengthBytes != 1 && lengthBytes != 2)
                throw new ArgumentException("Length prefix must be 1 or 2 bytes", nameof(lengthBytes));
            
            LengthBytes = lengthBytes;
        }
    }
}
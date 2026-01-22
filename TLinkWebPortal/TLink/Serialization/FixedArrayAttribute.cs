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
    /// Marks a byte array property as having a fixed length in the binary protocol.
    /// The array will always serialize to exactly the specified number of bytes.
    /// If the source array is shorter, it will be padded with zeros.
    /// If longer, it will be truncated.
    /// </summary>
    /// <example>
    /// [FixedArray(2)]
    /// public byte[] DeviceID { get; init; } = new byte[2];
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class FixedArrayAttribute : Attribute
    {
        /// <summary>
        /// The exact number of bytes this array occupies in the serialized format.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Create a fixed-length array attribute.
        /// </summary>
        /// <param name="length">Exact number of bytes in the serialized array</param>
        public FixedArrayAttribute(int length)
        {
            if (length <= 0)
                throw new ArgumentException("Fixed array length must be greater than zero", nameof(length));
            
            Length = length;
        }
    }
}
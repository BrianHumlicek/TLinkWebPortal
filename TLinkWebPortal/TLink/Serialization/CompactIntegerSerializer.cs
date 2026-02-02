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

using System.Reflection;

namespace DSC.TLink.Serialization
{
    /// <summary>
    /// Handles serialization of integers with [CompactInteger] attribute.
    /// Only stores significant bytes with a leading length prefix.
    /// </summary>
    internal class CompactIntegerSerializer : ITypeSerializer
    {
        public bool CanHandle(PropertyInfo property)
        {
            if (!property.IsDefined(typeof(CompactIntegerAttribute), false))
                return false;

            var type = property.PropertyType;
            var typeCode = Type.GetTypeCode(type);

            return typeCode switch
            {
                TypeCode.Byte or TypeCode.SByte or
                TypeCode.UInt16 or TypeCode.Int16 or
                TypeCode.UInt32 or TypeCode.Int32 or
                TypeCode.UInt64 or TypeCode.Int64 => true,
                _ => false
            };
        }

        public void Write(List<byte> bytes, PropertyInfo property, object? value)
        {
            var type = property.PropertyType;

            // Get the full bytes representation using PrimitiveSerializer
            byte[] fullBytes = PrimitiveSerializer.GetBytes(value ?? GetDefaultValue(type), type);

            // Find the first significant byte
            int startIndex = FindSignificantByteIndex(fullBytes, type);

            // Calculate length of significant bytes
            int length = fullBytes.Length - startIndex;

            if (length > 255)
                throw new InvalidOperationException(
                    $"Property '{property.Name}' compact integer length {length} exceeds 1-byte max (255)");

            // Write length prefix
            bytes.Add((byte)length);

            // Write significant bytes
            bytes.AddRange(fullBytes.AsSpan(startIndex));
        }

        public object Read(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int remainingBytes)
        {
            var type = property.PropertyType;

            // Read length prefix
            if (offset >= bytes.Length)
                throw new InvalidOperationException(
                    $"Not enough bytes to read length prefix for compact integer '{property.Name}'");

            int length = bytes[offset++];

            // Read the compact bytes
            if (offset + length > bytes.Length)
                throw new InvalidOperationException(
                    $"Not enough bytes to read compact integer '{property.Name}' (expected {length}, got {bytes.Length - offset})");

            var compactBytes = bytes.Slice(offset, length);
            offset += length;

            // Use PrimitiveSerializer to read with sign extension for signed types
            bool isSigned = IsSigned(type);
            return PrimitiveSerializer.ReadFromBytes(compactBytes, type, signExtend: isSigned);
        }

        private static int FindSignificantByteIndex(byte[] fullBytes, Type type)
        {
            int startIndex = 0;
            bool isSigned = IsSigned(type);

            if (isSigned)
            {
                // For signed types, keep the byte with the sign bit
                bool isNegative = (fullBytes[0] & 0x80) != 0;
                
                for (int i = 0; i < fullBytes.Length - 1; i++)
                {
                    byte currentByte = fullBytes[i];
                    byte nextByte = fullBytes[i + 1];
                    
                    if (isNegative)
                    {
                        if (currentByte == 0xFF && (nextByte & 0x80) != 0)
                            startIndex = i + 1;
                        else
                            break;
                    }
                    else
                    {
                        if (currentByte == 0x00 && (nextByte & 0x80) == 0)
                            startIndex = i + 1;
                        else
                            break;
                    }
                }
            }
            else
            {
                // For unsigned types, skip leading zeros
                while (startIndex < fullBytes.Length - 1 && fullBytes[startIndex] == 0)
                {
                    startIndex++;
                }
            }

            return startIndex;
        }

        private static bool IsSigned(Type type)
        {
            return Type.GetTypeCode(type) is 
                TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64;
        }

        private static object GetDefaultValue(Type type)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte => (byte)0,
                TypeCode.SByte => (sbyte)0,
                TypeCode.UInt16 => (ushort)0,
                TypeCode.Int16 => (short)0,
                TypeCode.UInt32 => (uint)0,
                TypeCode.Int32 => 0,
                TypeCode.UInt64 => (ulong)0,
                TypeCode.Int64 => (long)0,
                _ => throw new NotSupportedException()
            };
        }

        // Remove all the duplicate GetBytes* and Read* methods - use PrimitiveSerializer instead
    }
}
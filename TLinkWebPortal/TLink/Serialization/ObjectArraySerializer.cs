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
    /// Handles serialization of arrays of complex objects (records/classes).
    /// Supports [LeadingLengthArray] attribute to prefix the array with element count.
    /// Each element is recursively serialized using BinarySerializer.
    /// </summary>
    internal class ObjectArraySerializer : ITypeSerializer
    {
        public bool CanHandle(PropertyInfo property)
        {
            // Handle arrays of non-primitive types (excluding byte[] which is handled by ByteArraySerializer)
            if (!property.PropertyType.IsArray)
                return false;

            var elementType = property.PropertyType.GetElementType();
            if (elementType == null || elementType == typeof(byte))
                return false;

            // Handle complex types (classes/records) and non-primitive types
            return Type.GetTypeCode(elementType) == TypeCode.Object && !elementType.IsEnum;
        }

        public void Write(List<byte> bytes, PropertyInfo property, object? value)
        {
            var arr = value as Array ?? Array.CreateInstance(property.PropertyType.GetElementType()!, 0);
            
            // Check for LeadingLengthArray attribute
            var lengthAttr = property.GetCustomAttribute<LeadingLengthArrayAttribute>();
            if (lengthAttr != null)
            {
                WriteLeadingLengthArray(bytes, property, arr, lengthAttr.LengthBytes);
            }
            else
            {
                // No attribute = just write elements without count prefix
                WriteElements(bytes, arr);
            }
        }

        public object Read(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int remainingBytes)
        {
            var elementType = property.PropertyType.GetElementType()!;
            
            // Check for LeadingLengthArray attribute
            var lengthAttr = property.GetCustomAttribute<LeadingLengthArrayAttribute>();
            if (lengthAttr != null)
            {
                return ReadLeadingLengthArray(bytes, ref offset, property, elementType, lengthAttr.LengthBytes);
            }
            else
            {
                // No attribute = read until end of data (not recommended for object arrays)
                throw new InvalidOperationException(
                    $"Object array property '{property.Name}' must have [LeadingLengthArray] attribute. " +
                    "Unbounded object arrays are not supported.");
            }
        }

        private static void WriteLeadingLengthArray(List<byte> bytes, PropertyInfo property, Array arr, int lengthBytes)
        {
            int length = arr.Length;

            // Write length prefix
            switch (lengthBytes)
            {
                case 1:
                    if (length > 255)
                        throw new InvalidOperationException(
                            $"Property '{property.Name}' array length {length} exceeds 1-byte prefix max (255).");
                    bytes.Add((byte)length);
                    break;

                case 2:
                    if (length > 65535)
                        throw new InvalidOperationException(
                            $"Property '{property.Name}' array length {length} exceeds 2-byte prefix max (65535).");
                    bytes.Add((byte)(length >> 8));
                    bytes.Add((byte)(length & 0xFF));
                    break;

                default:
                    throw new InvalidOperationException($"Invalid length bytes {lengthBytes} for property '{property.Name}'");
            }

            // Write elements
            WriteElements(bytes, arr);
        }

        private static void WriteElements(List<byte> bytes, Array arr)
        {
            foreach (var element in arr)
            {
                if (element == null)
                    throw new InvalidOperationException("Cannot serialize null elements in object array");

                // Recursively serialize each element using BinarySerializer
                var elementBytes = BinarySerializer.Serialize(element);
                bytes.AddRange(elementBytes);
            }
        }

        private static Array ReadLeadingLengthArray(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, Type elementType, int lengthBytes)
        {
            // Read length prefix
            int length = lengthBytes switch
            {
                1 => ReadLengthPrefix1(bytes, ref offset, property),
                2 => ReadLengthPrefix2(bytes, ref offset, property),
                _ => throw new InvalidOperationException($"Invalid length prefix size {lengthBytes} for property '{property.Name}'")
            };

            // Create array
            var arr = Array.CreateInstance(elementType, length);

            // Read each element
            for (int i = 0; i < length; i++)
            {
                // Calculate element size by reading ahead (we need to know how many bytes each element consumes)
                // For now, we'll read greedily and let the element deserializer handle it
                var elementBytes = bytes.Slice(offset);
                
                // Deserialize element - need to track how many bytes were consumed
                int beforeOffset = offset;
                var element = DeserializeElement(elementBytes, ref offset, elementType);
                arr.SetValue(element, i);
            }

            return arr;
        }

        private static object DeserializeElement(ReadOnlySpan<byte> bytes, ref int offset, Type elementType)
        {
            // Create a temporary offset for the element deserialization
            int elementOffset = 0;
            
            // Get all properties to calculate element size
            var properties = elementType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && !p.IsDefined(typeof(IgnorePropertyAttribute), false))
                .OrderBy(p => p.MetadataToken)
                .ToArray();

            var instance = Activator.CreateInstance(elementType);
            if (instance == null)
                throw new InvalidOperationException($"Failed to create instance of type {elementType.FullName}");

            // Read each property of the element
            foreach (var prop in properties)
            {
                // Calculate remaining bytes for this property
                int remainingBytes = bytes.Length - elementOffset;
                
                // Read property using the appropriate serializer
                var value = ReadElementProperty(bytes, ref elementOffset, prop, remainingBytes);
                prop.SetValue(instance, value);
            }

            // Advance the main offset to the end of the element
            offset += elementOffset;

            return instance;
        }

        private static object ReadElementProperty(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int remainingBytes)
        {
            var type = property.PropertyType;

            // Handle byte arrays
            if (type == typeof(byte[]))
            {
                var lengthAttr = property.GetCustomAttribute<LeadingLengthArrayAttribute>();
                if (lengthAttr != null)
                {
                    int length = lengthAttr.LengthBytes == 1 ? bytes[offset++] : 
                                 PrimitiveSerializer.ReadUInt16(bytes, ref offset);
                    var arr = bytes.Slice(offset, length).ToArray();
                    offset += length;
                    return arr;
                }
                throw new InvalidOperationException($"Byte array property '{property.Name}' in nested object must have [LeadingLengthArray]");
            }

            // Handle primitives and enums using PrimitiveSerializer helpers
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte => bytes[offset++],
                TypeCode.SByte => (sbyte)bytes[offset++],
                TypeCode.UInt16 => PrimitiveSerializer.ReadUInt16(bytes, ref offset),
                TypeCode.Int16 => PrimitiveSerializer.ReadInt16(bytes, ref offset),
                TypeCode.UInt32 => PrimitiveSerializer.ReadUInt32(bytes, ref offset),
                TypeCode.Int32 => PrimitiveSerializer.ReadInt32(bytes, ref offset),
                TypeCode.Object when type.IsEnum => PrimitiveSerializer.ReadEnum(bytes, ref offset, type),
                _ => throw new NotSupportedException($"Type {type} not supported in nested object (property '{property.Name}')")
            };
        }

        private static int ReadLengthPrefix1(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property)
        {
            if (offset >= bytes.Length)
                throw new InvalidOperationException($"Not enough bytes to read length prefix for '{property.Name}'");
            return bytes[offset++];
        }

        private static int ReadLengthPrefix2(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property)
        {
            if (offset + 1 >= bytes.Length)
                throw new InvalidOperationException($"Not enough bytes to read length prefix for '{property.Name}'");
            return PrimitiveSerializer.ReadUInt16(bytes, ref offset);
        }
    }
}
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

using System.Collections.Concurrent;
using System.Reflection;
using DSC.TLink.ITv2.Messages;

namespace DSC.TLink.Serialization
{
    /// <summary>
    /// Simple sequential binary serializer for POCOs with primitive properties.
    /// Properties are serialized in declaration order.
    /// Supports [FixedArray] and [LeadingLengthArray] attributes for byte array serialization.
    /// </summary>
    internal static class BinarySerializer
    {
        // Cache property info to avoid repeated reflection
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

        /// <summary>
        /// Serialize a POCO to bytes. Properties must be primitives, enums, or byte arrays.
        /// Use [FixedArray] or [LeadingLengthArray] attributes to control array serialization.
        /// </summary>
        public static List<byte> Serialize(object value)
        {
            var bytes = new List<byte>();
            var properties = GetCachedProperties(value.GetType());

            foreach (var prop in properties)
            {
                var val = prop.GetValue(value);
                WriteProperty(bytes, prop, val);
            }

            return bytes;
        }

        /// <summary>
        /// Deserialize bytes into an IMessageData instance of the specified type.
        /// </summary>
        /// <param name="type">The concrete type to deserialize (must implement IMessageData and have parameterless constructor)</param>
        /// <param name="bytes">The byte span to deserialize from</param>
        /// <returns>Deserialized IMessageData instance</returns>
        public static IMessageData Deserialize(Type type, ReadOnlySpan<byte> bytes)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(IMessageData).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.FullName} must implement IMessageData", nameof(type));

            // Create instance using Activator
            var result = Activator.CreateInstance(type);
            if (result == null)
                throw new InvalidOperationException($"Failed to create instance of type {type.FullName}. Ensure it has a parameterless constructor.");

            var properties = GetCachedProperties(type);

            int offset = 0;
            foreach (var prop in properties)
            {
                var value = ReadProperty(bytes, ref offset, prop);
                prop.SetValue(result, value);
            }

            return (IMessageData)result;
        }

        /// <summary>
        /// Generic convenience method for deserializing when the type is known at compile time.
        /// </summary>
        public static T Deserialize<T>(ReadOnlySpan<byte> bytes) where T : class, IMessageData, new()
        {
            return (T)Deserialize(typeof(T), bytes);
        }

        private static PropertyInfo[] GetCachedProperties(Type type)
        {
            return _propertyCache.GetOrAdd(type, t =>
            {
                return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && !p.IsDefined(typeof(IgnorePropertyAttribute), false))
                    .OrderBy(p => p.MetadataToken) // Declaration order
                    .ToArray();
            });
        }

        private static void WriteProperty(List<byte> bytes, PropertyInfo property, object? value)
        {
            var type = property.PropertyType;

            // Handle byte arrays with FixedArray or LeadingLengthArray attributes
            if (type == typeof(byte[]))
            {
                WriteByteArray(bytes, property, (byte[]?)value ?? Array.Empty<byte>());
                return;
            }

            // Handle primitives and enums with switch expression
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                    bytes.Add((byte)(value ?? 0));
                    break;

                case TypeCode.SByte:
                    bytes.Add((byte)(sbyte)(value ?? 0));
                    break;

                case TypeCode.UInt16:
                    {
                        var val = (ushort)(value ?? 0);
                        bytes.Add((byte)(val >> 8));   // Big-endian
                        bytes.Add((byte)(val & 0xFF));
                    }
                    break;

                case TypeCode.Int16:
                    {
                        var val = (short)(value ?? 0);
                        bytes.Add((byte)(val >> 8));
                        bytes.Add((byte)(val & 0xFF));
                    }
                    break;

                case TypeCode.UInt32:
                    {
                        var val = (uint)(value ?? 0);
                        bytes.Add((byte)(val >> 24));
                        bytes.Add((byte)(val >> 16));
                        bytes.Add((byte)(val >> 8));
                        bytes.Add((byte)(val & 0xFF));
                    }
                    break;

                case TypeCode.Int32:
                    {
                        var val = (int)(value ?? 0);
                        bytes.Add((byte)(val >> 24));
                        bytes.Add((byte)(val >> 16));
                        bytes.Add((byte)(val >> 8));
                        bytes.Add((byte)(val & 0xFF));
                    }
                    break;

                case TypeCode.Object when type.IsEnum:
                    WriteEnum(bytes, type, value);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Type {type} not supported for binary serialization (property '{property.Name}')");
            }
        }

        private static void WriteByteArray(List<byte> bytes, PropertyInfo property, byte[] arr)
        {
            // Check for FixedArray attribute first
            var fixedAttr = property.GetCustomAttribute<FixedArrayAttribute>();
            if (fixedAttr != null)
            {
                WriteFixedArray(bytes, arr, fixedAttr.Length);
                return;
            }

            // Check for LeadingLengthArray attribute
            var lengthAttr = property.GetCustomAttribute<LeadingLengthArrayAttribute>();
            if (lengthAttr != null)
            {
                WriteLeadingLengthArray(bytes, property, arr, lengthAttr.LengthBytes);
                return;
            }

            // No attribute found - error
            throw new InvalidOperationException(
                $"Property '{property.Name}' is a byte array but missing [FixedArray] or [LeadingLengthArray] attribute. " +
                $"Specify [FixedArray(N)] for fixed-length or [LeadingLengthArray()] for variable-length arrays.");
        }

        private static void WriteFixedArray(List<byte> bytes, byte[] arr, int fixedLength)
        {
            // Fixed-length: write exactly N bytes, pad or truncate if needed
            if (arr.Length >= fixedLength)
            {
                bytes.AddRange(arr.Take(fixedLength));
            }
            else
            {
                bytes.AddRange(arr);
                // Pad with zeros if array is shorter than fixed length
                bytes.AddRange(Enumerable.Repeat((byte)0, fixedLength - arr.Length));
            }
        }

        private static void WriteLeadingLengthArray(List<byte> bytes, PropertyInfo property, byte[] arr, int lengthBytes)
        {
            // Write length prefix, then data
            switch (lengthBytes)
            {
                case 1:
                    if (arr.Length > 255)
                        throw new InvalidOperationException(
                            $"Property '{property.Name}' array length {arr.Length} exceeds 1-byte prefix max (255). Use [LeadingLengthArray(2)].");
                    bytes.Add((byte)arr.Length);
                    break;

                case 2:
                    if (arr.Length > 65535)
                        throw new InvalidOperationException(
                            $"Property '{property.Name}' array length {arr.Length} exceeds 2-byte prefix max (65535).");
                    bytes.Add((byte)(arr.Length >> 8));   // Big-endian
                    bytes.Add((byte)(arr.Length & 0xFF));
                    break;
            }
            bytes.AddRange(arr);
        }

        private static void WriteEnum(List<byte> bytes, Type type, object? value)
        {
            var underlyingType = Enum.GetUnderlyingType(type);

            switch (Type.GetTypeCode(underlyingType))
            {
                case TypeCode.Byte:
                    bytes.Add((byte)(value ?? 0));
                    break;

                case TypeCode.UInt16:
                    {
                        var val = (ushort)(value ?? 0);
                        bytes.Add((byte)(val >> 8));
                        bytes.Add((byte)(val & 0xFF));
                    }
                    break;

                default:
                    throw new NotSupportedException(
                        $"Enum underlying type {underlyingType} not supported");
            }
        }

        private static object ReadProperty(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property)
        {
            var type = property.PropertyType;

            // Handle byte arrays
            if (type == typeof(byte[]))
            {
                return ReadByteArray(bytes, ref offset, property);
            }

            // Handle primitives and enums with switch expression
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte => bytes[offset++],

                TypeCode.SByte => (sbyte)bytes[offset++],

                TypeCode.UInt16 => ReadUInt16(bytes, ref offset),

                TypeCode.Int16 => ReadInt16(bytes, ref offset),

                TypeCode.UInt32 => ReadUInt32(bytes, ref offset),

                TypeCode.Int32 => ReadInt32(bytes, ref offset),

                TypeCode.Object when type.IsEnum => ReadEnum(bytes, ref offset, type),

                _ => throw new NotSupportedException(
                    $"Type {type} not supported for binary deserialization (property '{property.Name}')")
            };
        }

        private static byte[] ReadByteArray(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property)
        {
            // Check for FixedArray attribute first
            var fixedAttr = property.GetCustomAttribute<FixedArrayAttribute>();
            if (fixedAttr != null)
            {
                return ReadFixedArray(bytes, ref offset, property, fixedAttr.Length);
            }

            // Check for LeadingLengthArray attribute
            var lengthAttr = property.GetCustomAttribute<LeadingLengthArrayAttribute>();
            if (lengthAttr != null)
            {
                return ReadLeadingLengthArray(bytes, ref offset, property, lengthAttr.LengthBytes);
            }

            // No attribute found - error
            throw new InvalidOperationException(
                $"Property '{property.Name}' is a byte array but missing [FixedArray] or [LeadingLengthArray] attribute. " +
                $"Specify [FixedArray(N)] for fixed-length or [LeadingLengthArray()] for variable-length arrays.");
        }

        private static byte[] ReadFixedArray(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int fixedLength)
        {
            if (offset + fixedLength > bytes.Length)
                throw new InvalidOperationException(
                    $"Not enough bytes to read fixed array '{property.Name}' (need {fixedLength}, have {bytes.Length - offset})");

            var arr = bytes.Slice(offset, fixedLength).ToArray();
            offset += fixedLength;
            return arr;
        }

        private static byte[] ReadLeadingLengthArray(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int lengthBytes)
        {
            int length = lengthBytes switch
            {
                1 => ReadLengthPrefix1(bytes, ref offset, property),
                2 => ReadLengthPrefix2(bytes, ref offset, property),
                _ => throw new InvalidOperationException($"Invalid length prefix size {lengthBytes} for property '{property.Name}'")
            };

            if (offset + length > bytes.Length)
                throw new InvalidOperationException(
                    $"Not enough bytes to read variable array '{property.Name}' (need {length}, have {bytes.Length - offset})");

            var arr = bytes.Slice(offset, length).ToArray();
            offset += length;
            return arr;
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
                throw new InvalidOperationException($"Not enough bytes to read 2-byte length prefix for '{property.Name}'");
            var length = (bytes[offset] << 8) | bytes[offset + 1];
            offset += 2;
            return length;
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
            offset += 2;
            return val;
        }

        private static short ReadInt16(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (short)((bytes[offset] << 8) | bytes[offset + 1]);
            offset += 2;
            return val;
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) |
                             (bytes[offset + 2] << 8) | bytes[offset + 3]);
            offset += 4;
            return val;
        }

        private static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (bytes[offset] << 24) | (bytes[offset + 1] << 16) |
                      (bytes[offset + 2] << 8) | bytes[offset + 3];
            offset += 4;
            return val;
        }

        private static object ReadEnum(ReadOnlySpan<byte> bytes, ref int offset, Type type)
        {
            var underlyingType = Enum.GetUnderlyingType(type);

            return Type.GetTypeCode(underlyingType) switch
            {
                TypeCode.Byte => Enum.ToObject(type, bytes[offset++]),

                TypeCode.UInt16 => Enum.ToObject(type, ReadUInt16(bytes, ref offset)),

                _ => throw new NotSupportedException(
                    $"Enum underlying type {underlyingType} not supported")
            };
        }

        /// <summary>
        /// Clear the property cache. Useful for testing or if types are dynamically modified.
        /// </summary>
        public static void ClearCache() => _propertyCache.Clear();
    }

    /// <summary>
    /// Mark properties to exclude from binary serialization (e.g., calculated properties).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnorePropertyAttribute : Attribute { }
}
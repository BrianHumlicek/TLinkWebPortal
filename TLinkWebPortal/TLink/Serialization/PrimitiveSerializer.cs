// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Reflection;

namespace DSC.TLink.Serialization
{
    /// <summary>
    /// Handles primitive types (byte, short, int, etc.) and enums.
    /// Also provides static helper methods for other serializers to use.
    /// </summary>
    internal class PrimitiveSerializer : ITypeSerializer
    {
        public bool CanHandle(PropertyInfo property)
        {
            var type = property.PropertyType;
            return Type.GetTypeCode(type) != TypeCode.Object || type.IsEnum;
        }

        public void Write(List<byte> bytes, PropertyInfo property, object? value)
        {
            WritePrimitive(bytes, property, value);
        }

        public object Read(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int remainingBytes)
        {
            return ReadPrimitive(bytes, ref offset, property);
        }

        internal static void WritePrimitive(List<byte> bytes, PropertyInfo property, object? value)
        {
            var type = property.PropertyType;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                    bytes.Add((byte)(value ?? 0));
                    break;

                case TypeCode.SByte:
                    bytes.Add((byte)(sbyte)(value ?? 0));
                    break;

                case TypeCode.UInt16:
                    WriteUInt16(bytes, (ushort)(value ?? 0));
                    break;

                case TypeCode.Int16:
                    WriteInt16(bytes, (short)(value ?? 0));
                    break;

                case TypeCode.UInt32:
                    WriteUInt32(bytes, (uint)(value ?? 0));
                    break;

                case TypeCode.Int32:
                    WriteInt32(bytes, (int)(value ?? 0));
                    break;

                case TypeCode.Object when type.IsEnum:
                    WriteEnum(bytes, type, value);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Type {type} not supported for binary serialization (property '{property.Name}')");
            }
        }

        internal static object ReadPrimitive(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property)
        {
            var type = property.PropertyType;

            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte => bytes[offset++],
                TypeCode.SByte => (sbyte)bytes[offset++],
                TypeCode.UInt16 => ReadUInt16(bytes, ref offset),
                TypeCode.Int16 => ReadInt16(bytes, ref offset),
                TypeCode.UInt32 => ReadUInt32(bytes, ref offset),
                TypeCode.Int32 => ReadInt32(bytes, ref offset),
                TypeCode.Object when type.IsEnum => ReadEnum(bytes, ref offset, type),
                _ => throw new NotSupportedException($"Type {type} not supported (property '{property.Name}')")
            };
        }

        // Public static helper methods for other serializers to use
        #region Write Helpers

        internal static void WriteUInt16(List<byte> bytes, ushort value)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value & 0xFF));
        }

        internal static void WriteInt16(List<byte> bytes, short value)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value & 0xFF));
        }

        internal static void WriteUInt32(List<byte> bytes, uint value)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value & 0xFF));
        }

        internal static void WriteInt32(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)(value & 0xFF));
        }

        internal static void WriteEnum(List<byte> bytes, Type type, object? value)
        {
            var underlyingType = Enum.GetUnderlyingType(type);

            switch (Type.GetTypeCode(underlyingType))
            {
                case TypeCode.Byte:
                    bytes.Add((byte)(value ?? 0));
                    break;

                case TypeCode.UInt16:
                    WriteUInt16(bytes, (ushort)(value ?? 0));
                    break;

                default:
                    throw new NotSupportedException($"Enum underlying type {underlyingType} not supported");
            }
        }

        #endregion

        #region Read Helpers

        internal static ushort ReadUInt16(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (ushort)((bytes[offset] << 8) | bytes[offset + 1]);
            offset += 2;
            return val;
        }

        internal static short ReadInt16(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (short)((bytes[offset] << 8) | bytes[offset + 1]);
            offset += 2;
            return val;
        }

        internal static uint ReadUInt32(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (uint)((bytes[offset] << 24) | (bytes[offset + 1] << 16) |
                             (bytes[offset + 2] << 8) | bytes[offset + 3]);
            offset += 4;
            return val;
        }

        internal static int ReadInt32(ReadOnlySpan<byte> bytes, ref int offset)
        {
            var val = (bytes[offset] << 24) | (bytes[offset + 1] << 16) |
                      (bytes[offset + 2] << 8) | bytes[offset + 3];
            offset += 4;
            return val;
        }

        internal static object ReadEnum(ReadOnlySpan<byte> bytes, ref int offset, Type type)
        {
            var underlyingType = Enum.GetUnderlyingType(type);

            return Type.GetTypeCode(underlyingType) switch
            {
                TypeCode.Byte => Enum.ToObject(type, bytes[offset++]),
                TypeCode.UInt16 => Enum.ToObject(type, ReadUInt16(bytes, ref offset)),
                _ => throw new NotSupportedException($"Enum underlying type {underlyingType} not supported")
            };
        }

        #endregion

        #region Byte Array Helpers (for CompactInteger and other specialized serializers)

        /// <summary>
        /// Convert integer value to big-endian byte array.
        /// </summary>
        internal static byte[] GetBytes(object value, Type type)
        {
            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte => new[] { (byte)value },
                TypeCode.SByte => new[] { (byte)(sbyte)value },
                TypeCode.UInt16 => GetBytesUInt16((ushort)value),
                TypeCode.Int16 => GetBytesInt16((short)value),
                TypeCode.UInt32 => GetBytesUInt32((uint)value),
                TypeCode.Int32 => GetBytesInt32((int)value),
                TypeCode.UInt64 => GetBytesUInt64((ulong)value),
                TypeCode.Int64 => GetBytesInt64((long)value),
                _ => throw new NotSupportedException($"Type {type} not supported for integer serialization")
            };
        }

        internal static byte[] GetBytesUInt16(ushort value) =>
            new[] { (byte)(value >> 8), (byte)(value & 0xFF) };

        internal static byte[] GetBytesInt16(short value) =>
            new[] { (byte)(value >> 8), (byte)(value & 0xFF) };

        internal static byte[] GetBytesUInt32(uint value) =>
            new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF) };

        internal static byte[] GetBytesInt32(int value) =>
            new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF) };

        internal static byte[] GetBytesUInt64(ulong value) => new[]
        {
            (byte)(value >> 56), (byte)(value >> 48), (byte)(value >> 40), (byte)(value >> 32),
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF)
        };

        internal static byte[] GetBytesInt64(long value) => new[]
        {
            (byte)(value >> 56), (byte)(value >> 48), (byte)(value >> 40), (byte)(value >> 32),
            (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)(value & 0xFF)
        };

        /// <summary>
        /// Read integer from byte span with optional sign extension.
        /// </summary>
        internal static object ReadFromBytes(ReadOnlySpan<byte> bytes, Type type, bool signExtend = false)
        {
            bool isNegative = signExtend && bytes.Length > 0 && (bytes[0] & 0x80) != 0;

            return Type.GetTypeCode(type) switch
            {
                TypeCode.Byte => bytes.Length == 0 ? (byte)0 : bytes[0],
                TypeCode.SByte => bytes.Length == 0 ? (sbyte)0 : (sbyte)bytes[0],
                TypeCode.UInt16 => ReadCompactUInt16(bytes),
                TypeCode.Int16 => ReadCompactInt16(bytes, isNegative),
                TypeCode.UInt32 => ReadCompactUInt32(bytes),
                TypeCode.Int32 => ReadCompactInt32(bytes, isNegative),
                TypeCode.UInt64 => ReadCompactUInt64(bytes),
                TypeCode.Int64 => ReadCompactInt64(bytes, isNegative),
                _ => throw new NotSupportedException($"Type {type} not supported")
            };
        }

        // Compact read helpers (handle variable-length integers with sign extension)
        private static ushort ReadCompactUInt16(ReadOnlySpan<byte> bytes)
        {
            ushort result = 0;
            for (int i = 0; i < bytes.Length && i < 2; i++)
                result = (ushort)((result << 8) | bytes[i]);
            return result;
        }

        private static short ReadCompactInt16(ReadOnlySpan<byte> bytes, bool isNegative)
        {
            short result = isNegative ? (short)-1 : (short)0;
            for (int i = 0; i < bytes.Length && i < 2; i++)
                result = (short)((result << 8) | bytes[i]);
            return result;
        }

        private static uint ReadCompactUInt32(ReadOnlySpan<byte> bytes)
        {
            uint result = 0;
            for (int i = 0; i < bytes.Length && i < 4; i++)
                result = (result << 8) | bytes[i];
            return result;
        }

        private static int ReadCompactInt32(ReadOnlySpan<byte> bytes, bool isNegative)
        {
            int result = isNegative ? -1 : 0;
            for (int i = 0; i < bytes.Length && i < 4; i++)
                result = (result << 8) | bytes[i];
            return result;
        }

        private static ulong ReadCompactUInt64(ReadOnlySpan<byte> bytes)
        {
            ulong result = 0;
            for (int i = 0; i < bytes.Length && i < 8; i++)
                result = (result << 8) | bytes[i];
            return result;
        }

        private static long ReadCompactInt64(ReadOnlySpan<byte> bytes, bool isNegative)
        {
            long result = isNegative ? -1L : 0L;
            for (int i = 0; i < bytes.Length && i < 8; i++)
                result = (result << 8) | bytes[i];
            return result;
        }

        #endregion
    }
}
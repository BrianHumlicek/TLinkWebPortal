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
    /// Handles serialization of byte arrays with different strategies:
    /// - [FixedArray(N)]: Fixed-length array (padded/truncated)
    /// - [LeadingLengthArray(N)]: Length-prefixed array
    /// - No attribute: Unbounded array (consumes all remaining bytes, must be last property)
    /// </summary>
    internal class ByteArraySerializer : ITypeSerializer
    {
        public bool CanHandle(PropertyInfo property)
        {
            return property.PropertyType == typeof(byte[]);
        }

        public void Write(List<byte> bytes, PropertyInfo property, object? value)
        {
            var arr = (byte[]?)value ?? Array.Empty<byte>();

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

            // No attribute = unbounded array (just write all bytes)
            bytes.AddRange(arr);
        }

        public object Read(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int remainingBytes)
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

            // No attribute = unbounded array (consume all remaining bytes)
            return ReadUnboundedArray(bytes, ref offset, remainingBytes);
        }

        private static void WriteFixedArray(List<byte> bytes, byte[] arr, int fixedLength)
        {
            if (arr.Length >= fixedLength)
            {
                bytes.AddRange(arr.Take(fixedLength));
            }
            else
            {
                bytes.AddRange(arr);
                bytes.AddRange(Enumerable.Repeat((byte)0, fixedLength - arr.Length));
            }
        }

        private static void WriteLeadingLengthArray(List<byte> bytes, PropertyInfo property, byte[] arr, int lengthBytes)
        {
            switch (lengthBytes)
            {
                case 1:
                    if (arr.Length > 255)
                        throw new InvalidOperationException(
                            $"Property '{property.Name}' array length {arr.Length} exceeds 1-byte prefix max (255).");
                    bytes.Add((byte)arr.Length);
                    break;

                case 2:
                    if (arr.Length > 65535)
                        throw new InvalidOperationException(
                            $"Property '{property.Name}' array length {arr.Length} exceeds 2-byte prefix max (65535).");
                    bytes.Add((byte)(arr.Length >> 8));
                    bytes.Add((byte)(arr.Length & 0xFF));
                    break;

                default:
                    throw new InvalidOperationException($"Invalid length bytes {lengthBytes} for property '{property.Name}'");
            }
            bytes.AddRange(arr);
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

        private static byte[] ReadUnboundedArray(ReadOnlySpan<byte> bytes, ref int offset, int remainingBytes)
        {
            // Consume all remaining bytes
            var arr = bytes.Slice(offset, remainingBytes).ToArray();
            offset += remainingBytes;
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
    }
}
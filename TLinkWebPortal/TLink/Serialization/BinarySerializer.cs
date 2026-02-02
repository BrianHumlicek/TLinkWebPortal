// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Collections.Concurrent;
using System.Reflection;
using DSC.TLink.ITv2.Messages;

namespace DSC.TLink.Serialization
{
    /// <summary>
    /// Modular binary serializer for POCOs.
    /// Uses strategy pattern with pluggable ITypeSerializer implementations.
    /// </summary>
    internal static class BinarySerializer
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
        private static readonly List<ITypeSerializer> _typeSerializers = new()
        {
            new MultipleMessagePacketSerializer(), // Most specific handler
            new CompactIntegerSerializer(),        // Add before ObjectArraySerializer
            new ObjectArraySerializer(),
            new ByteArraySerializer(),
            new DateTimeSerializer(),
            new PrimitiveSerializer()
        };

        /// <summary>
        /// Serialize a POCO to bytes. Properties must be primitives, enums, or byte arrays.
        /// Use [FixedArray] or [LeadingLengthArray] attributes to control array serialization.
        /// </summary>
        public static List<byte> Serialize(object value)
        {
            var bytes = new List<byte>();
            var properties = GetCachedProperties(value.GetType());

            // Group properties by bit field groups
            var (normalProps, bitFieldGroups) = GroupProperties(properties);

            int propIndex = 0;
            int bitFieldIndex = 0;

            // Process properties in declaration order, handling bit field groups as single units
            foreach (var prop in properties)
            {
                if (prop.IsDefined(typeof(BitFieldAttribute), false))
                {
                    // Check if this is the first property of its bit field group
                    var attr = prop.GetCustomAttribute<BitFieldAttribute>()!;
                    if (bitFieldGroups.TryGetValue(attr.GroupName, out var group) && group[0] == prop)
                    {
                        BitFieldSerializer.WriteBitFieldGroup(bytes, group, value);
                    }
                    // Skip other properties in the same group (already written)
                }
                else
                {
                    WriteProperty(bytes, prop, prop.GetValue(value));
                }
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
            var (normalProps, bitFieldGroups) = GroupProperties(properties);

            int offset = 0;
            
            foreach (var prop in properties)
            {
                if (prop.IsDefined(typeof(BitFieldAttribute), false))
                {
                    var attr = prop.GetCustomAttribute<BitFieldAttribute>()!;
                    if (bitFieldGroups.TryGetValue(attr.GroupName, out var group) && group[0] == prop)
                    {
                        BitFieldSerializer.ReadBitFieldGroup(bytes, ref offset, group, result);
                    }
                }
                else
                {
                    int remainingBytes = bytes.Length - offset;
                    var value = ReadProperty(bytes, ref offset, prop, remainingBytes);
                    prop.SetValue(result, value);
                }
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

        internal static PropertyInfo[] GetCachedProperties(Type type)
        {
            return _propertyCache.GetOrAdd(type, t =>
            {
                return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && !p.IsDefined(typeof(IgnorePropertyAttribute), false))
                    .OrderBy(p => p.MetadataToken) // Declaration order
                    .ToArray();
            });
        }

        private static (PropertyInfo[] normal, Dictionary<string, PropertyInfo[]> bitFieldGroups) GroupProperties(PropertyInfo[] properties)
        {
            var bitFieldProps = properties
                .Where(p => p.IsDefined(typeof(BitFieldAttribute), false))
                .ToArray();

            var bitFieldGroups = bitFieldProps
                .GroupBy(p => p.GetCustomAttribute<BitFieldAttribute>()!.GroupName)
                .ToDictionary(g => g.Key, g => g.ToArray());

            var normalProps = properties.Except(bitFieldProps).ToArray();

            return (normalProps, bitFieldGroups);
        }

        private static void WriteProperty(List<byte> bytes, PropertyInfo property, object? value)
        {
            // Try registered type serializers
            foreach (var serializer in _typeSerializers)
            {
                if (serializer.CanHandle(property))
                {
                    serializer.Write(bytes, property, value);
                    return;
                }
            }

            // Fallback to primitive serializer
            PrimitiveSerializer.WritePrimitive(bytes, property, value);
        }

        private static object ReadProperty(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int remainingBytes)
        {
            // Try registered type serializers
            foreach (var serializer in _typeSerializers)
            {
                if (serializer.CanHandle(property))
                {
                    return serializer.Read(bytes, ref offset, property, remainingBytes);
                }
            }

            // Fallback to primitive serializer
            return PrimitiveSerializer.ReadPrimitive(bytes, ref offset, property);
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
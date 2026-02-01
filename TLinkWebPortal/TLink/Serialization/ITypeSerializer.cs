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
    /// Interface for type-specific serializers.
    /// Each implementation handles serialization/deserialization for a specific type or category of types.
    /// </summary>
    internal interface ITypeSerializer
    {
        /// <summary>
        /// Check if this serializer can handle the given property type.
        /// </summary>
        bool CanHandle(PropertyInfo property);

        /// <summary>
        /// Write the property value to the byte list.
        /// </summary>
        void Write(List<byte> bytes, PropertyInfo property, object? value);

        /// <summary>
        /// Read the property value from the byte span.
        /// </summary>
        /// <param name="bytes">Source bytes</param>
        /// <param name="offset">Current offset (will be advanced)</param>
        /// <param name="property">Property metadata</param>
        /// <param name="remainingBytes">Total remaining bytes available (for unbounded arrays)</param>
        /// <returns>Deserialized value</returns>
        object Read(ReadOnlySpan<byte> bytes, ref int offset, PropertyInfo property, int remainingBytes);
    }
}
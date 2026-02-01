// DSC TLink - a communications library for DSC Powerseries NEO alarm panels
// Copyright (C) 2024 Brian Humlicek
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

namespace DSC.TLink.Serialization
{
    /// <summary>
    /// Extension methods for bit field extraction and insertion on primitive types.
    /// </summary>
    public static class BitFieldExtensions
    {
        #region Byte Extensions

        /// <summary>
        /// Extract bits from this byte value.
        /// </summary>
        /// <param name="value">Source byte value</param>
        /// <param name="bitPosition">Zero-based starting bit position (0 = LSB)</param>
        /// <param name="bitWidth">Number of bits to extract (1-8)</param>
        /// <returns>Extracted value as int</returns>
        public static int ExtractBits(this byte value, int bitPosition, int bitWidth)
        {
            ValidateBitParameters(bitPosition, bitWidth, 8);
            uint mask = ((1u << bitWidth) - 1) << bitPosition;
            return (int)((value & mask) >> bitPosition);
        }

        /// <summary>
        /// Insert bits into this byte value.
        /// </summary>
        /// <param name="targetValue">Target byte value to modify</param>
        /// <param name="bitsToInsert">Value to insert (will be masked to fit bitWidth)</param>
        /// <param name="bitPosition">Zero-based starting bit position (0 = LSB)</param>
        /// <param name="bitWidth">Number of bits to insert (1-8)</param>
        /// <returns>Modified byte value</returns>
        public static byte InsertBits(this byte targetValue, int bitsToInsert, int bitPosition, int bitWidth)
        {
            ValidateBitParameters(bitPosition, bitWidth, 8);
            uint mask = ((1u << bitWidth) - 1) << bitPosition;
            uint cleared = targetValue & ~mask;
            uint inserted = ((uint)bitsToInsert << bitPosition) & mask;
            return (byte)(cleared | inserted);
        }

        #endregion

        #region UInt16 Extensions

        /// <summary>
        /// Extract bits from this ushort value.
        /// </summary>
        /// <param name="value">Source ushort value</param>
        /// <param name="bitPosition">Zero-based starting bit position (0 = LSB)</param>
        /// <param name="bitWidth">Number of bits to extract (1-16)</param>
        /// <returns>Extracted value as int</returns>
        public static int ExtractBits(this ushort value, int bitPosition, int bitWidth)
        {
            ValidateBitParameters(bitPosition, bitWidth, 16);
            uint mask = ((1u << bitWidth) - 1) << bitPosition;
            return (int)((value & mask) >> bitPosition);
        }

        /// <summary>
        /// Insert bits into this ushort value.
        /// </summary>
        /// <param name="targetValue">Target ushort value to modify</param>
        /// <param name="bitsToInsert">Value to insert (will be masked to fit bitWidth)</param>
        /// <param name="bitPosition">Zero-based starting bit position (0 = LSB)</param>
        /// <param name="bitWidth">Number of bits to insert (1-16)</param>
        /// <returns>Modified ushort value</returns>
        public static ushort InsertBits(this ushort targetValue, int bitsToInsert, int bitPosition, int bitWidth)
        {
            ValidateBitParameters(bitPosition, bitWidth, 16);
            uint mask = ((1u << bitWidth) - 1) << bitPosition;
            uint cleared = targetValue & ~mask;
            uint inserted = ((uint)bitsToInsert << bitPosition) & mask;
            return (ushort)(cleared | inserted);
        }

        #endregion

        #region UInt32 Extensions

        /// <summary>
        /// Extract bits from this uint value.
        /// </summary>
        /// <param name="value">Source uint value</param>
        /// <param name="bitPosition">Zero-based starting bit position (0 = LSB)</param>
        /// <param name="bitWidth">Number of bits to extract (1-32)</param>
        /// <returns>Extracted value as int</returns>
        public static int ExtractBits(this uint value, int bitPosition, int bitWidth)
        {
            ValidateBitParameters(bitPosition, bitWidth, 32);
            uint mask = ((1u << bitWidth) - 1) << bitPosition;
            return (int)((value & mask) >> bitPosition);
        }

        /// <summary>
        /// Insert bits into this uint value.
        /// </summary>
        /// <param name="targetValue">Target uint value to modify</param>
        /// <param name="bitsToInsert">Value to insert (will be masked to fit bitWidth)</param>
        /// <param name="bitPosition">Zero-based starting bit position (0 = LSB)</param>
        /// <param name="bitWidth">Number of bits to insert (1-32)</param>
        /// <returns>Modified uint value</returns>
        public static uint InsertBits(this uint targetValue, int bitsToInsert, int bitPosition, int bitWidth)
        {
            ValidateBitParameters(bitPosition, bitWidth, 32);
            uint mask = ((1u << bitWidth) - 1) << bitPosition;
            uint cleared = targetValue & ~mask;
            uint inserted = ((uint)bitsToInsert << bitPosition) & mask;
            return cleared | inserted;
        }

        #endregion

        #region Validation

        private static void ValidateBitParameters(int bitPosition, int bitWidth, int maxBits)
        {
            if (bitPosition < 0 || bitPosition >= maxBits)
                throw new ArgumentOutOfRangeException(nameof(bitPosition), 
                    $"Bit position must be between 0 and {maxBits - 1}");
            
            if (bitWidth <= 0 || bitWidth > maxBits)
                throw new ArgumentOutOfRangeException(nameof(bitWidth), 
                    $"Bit width must be between 1 and {maxBits}");
            
            if (bitPosition + bitWidth > maxBits)
                throw new ArgumentException(
                    $"Bit position ({bitPosition}) + bit width ({bitWidth}) exceeds maximum size ({maxBits})");
        }

        #endregion
    }
}
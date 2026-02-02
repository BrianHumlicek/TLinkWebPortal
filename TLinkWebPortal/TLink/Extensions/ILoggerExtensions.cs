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

using DSC.TLink.ITv2.Messages;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DSC.TLink.Extensions
{
	public static class ILoggerExtensions
	{
		//These are intended to be a temporary solution to logging the data that is passed as arrays and sequences.
		//Ideally, I think there should be some kind of logger that can handle these structures or allow a custom
		//output format.
		public static void LogDebug(this ILogger log, string message, ReadOnlySequence<byte> sequence)
		{
			if (log.IsEnabled(LogLevel.Debug))
			{
				log.LogDebug(message, sequence.ToArray());
			}
		}
        public static void LogDebug(this ILogger log, string message, IEnumerable<byte> bytes)
		{
			if (log.IsEnabled(LogLevel.Debug))
			{
                var details = Enumerable2HexString(bytes);
                log.LogDebug(message, details);
			}
		}
        public static void LogTrace(this ILogger log, string message, IEnumerable<byte> bytes)
        {
            if (log.IsEnabled(LogLevel.Trace))
            {
                log.LogTrace(message, Enumerable2HexString(bytes));
            }
        }
        public static void LogTrace(this ILogger log, Func<string> message)
		{
			if (log.IsEnabled(LogLevel.Trace))
			{
				log.LogTrace(message());
			}
		}

		/// <summary>
		/// Logs an IMessageData object with its type and all property values.
		/// </summary>
		internal static void LogMessage(this ILogger log, LogLevel logLevel, string prefix, IMessageData message)
		{
			if (!log.IsEnabled(logLevel))
			{
				return;
			}

			var sb = new StringBuilder();
			var messageType = message.GetType();
			
			sb.AppendLine($"{prefix} [{messageType.Name}] ");

			var properties = messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (var prop in properties)
			{
				var value = prop.GetValue(message);
				var formattedValue = FormatPropertyValue(value, indentLevel: 1);
                sb.Append($"     {prop.Name} = {formattedValue}");
				if (!formattedValue.Contains('\n'))
				{
					sb.AppendLine();
				}
			}

			log.Log(logLevel, sb.ToString());
		}

		/// <summary>
		/// Logs an IMessageData object at Debug level.
		/// </summary>
		internal static void LogMessageDebug(this ILogger log, string prefix, IMessageData message)
			=> LogMessage(log, LogLevel.Debug, prefix, message);

		/// <summary>
		/// Logs an IMessageData object at Trace level.
		/// </summary>
		internal static void LogMessageTrace(this ILogger log, string prefix, IMessageData message)
			=> LogMessage(log, LogLevel.Trace, prefix, message);

		private static string FormatPropertyValue(object? value, int indentLevel = 0)
		{
			return value switch
			{
				null => "null",
				byte[] bytes => Enumerable2HexString(bytes),
				IEnumerable<byte> bytes => Enumerable2HexString(bytes),
				string str => $"\"{str}\"",
				IMessageData[] messages => FormatMessageArray(messages, indentLevel),
				Array array when IsComplexObjectArray(array) => FormatObjectArray(array, indentLevel),
				_ => value?.ToString() ?? "null"
			};
		}

		private static bool IsComplexObjectArray(Array array)
		{
			var elementType = array.GetType().GetElementType();
			if (elementType == null || elementType == typeof(byte))
				return false;

			// Check if it's a complex type (class/record/struct with properties)
			var typeCode = Type.GetTypeCode(elementType);
			return typeCode == TypeCode.Object && !elementType.IsEnum;
		}

		private static string FormatMessageArray(IMessageData[] messages, int indentLevel)
		{
			if (messages.Length == 0)
				return "[]";

			var sb = new StringBuilder();
			sb.AppendLine($"[{messages.Length} messages]");

			for (int i = 0; i < messages.Length; i++)
			{
				var message = messages[i];
				var indent = new string(' ', (indentLevel + 1) * 5);
				sb.AppendLine($"{indent}[{i}] {message.GetType().Name}");

				var properties = message.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
				foreach (var prop in properties)
				{
					var propValue = prop.GetValue(message);
					var formattedValue = FormatPropertyValue(propValue, indentLevel + 2);
					sb.Append($"{indent}     {prop.Name} = {formattedValue}");
					if (!formattedValue.Contains('\n'))
					{
						sb.AppendLine();
					}
				}
			}

			return sb.ToString();
		}

		private static string FormatObjectArray(Array array, int indentLevel)
		{
			if (array.Length == 0)
				return "[]";

			var elementType = array.GetType().GetElementType()!;
			var sb = new StringBuilder();
			sb.AppendLine($"[{array.Length} {elementType.Name}]");

			var indent = new string(' ', (indentLevel + 1) * 5);

			for (int i = 0; i < array.Length; i++)
			{
				var element = array.GetValue(i);
				if (element == null)
				{
					sb.AppendLine($"{indent}[{i}] null");
					continue;
				}

				sb.AppendLine($"{indent}[{i}]");

				var properties = element.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
				foreach (var prop in properties)
				{
					var propValue = prop.GetValue(element);
					var formattedValue = FormatPropertyValue(propValue, indentLevel + 2);
					sb.Append($"{indent}     {prop.Name} = {formattedValue}");
					if (!formattedValue.Contains('\n'))
					{
						sb.AppendLine();
					}
				}
			}

			return sb.ToString();
		}

		public static byte[] HexString2Array(string hexString) => hexString.Split('-').Select(s => byte.Parse(s, NumberStyles.HexNumber)).ToArray();
        public static string Enumerable2HexString(IEnumerable<byte> bytes)
        {
            var hexValues = String.Join('-', bytes.Select(b => $"{b:X2}"));
            return $"[{hexValues}]";            
        }
	}
}
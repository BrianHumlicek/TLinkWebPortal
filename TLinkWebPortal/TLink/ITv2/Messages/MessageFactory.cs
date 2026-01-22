using DSC.TLink.ITv2.Enumerations;
using DSC.TLink.Serialization;
using MemoryPack;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace DSC.TLink.ITv2.Messages
{
    internal static class MessageFactory
    {
        private static readonly ImmutableDictionary<ITv2Command, Type> _commandToType;
        private static readonly ImmutableDictionary<Type, ITv2Command> _typeToCommand;

        static MessageFactory()
        {
            var commandToTypeBuilder = ImmutableDictionary.CreateBuilder<ITv2Command, Type>();
            var typeToCommandBuilder = ImmutableDictionary.CreateBuilder<Type, ITv2Command>();

            var assembly = Assembly.GetExecutingAssembly();
            var messageDataTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IMessageData).IsAssignableFrom(t));

            foreach (var type in messageDataTypes)
            {
                var attribute = type.GetCustomAttribute<ITv2CommandAttribute>(inherit: false);
                if (attribute != null)
                {
                    var command = attribute.Command;
                    
                    if (commandToTypeBuilder.ContainsKey(command))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate ITv2CommandAttribute found for command '{command}'. " +
                            $"Types '{commandToTypeBuilder[command].FullName}' and '{type.FullName}' both declare this command.");
                    }

                    commandToTypeBuilder[command] = type;
                    typeToCommandBuilder[type] = command;
                }
            }

            _commandToType = commandToTypeBuilder.ToImmutable();
            _typeToCommand = typeToCommandBuilder.ToImmutable();
        }

        /// <summary>
        /// Deserialize bytes into a strongly-typed message object.
        /// </summary>
        public static IMessageData DeserializeMessage(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 2)
                throw new ArgumentException("Message too short to contain command", nameof(bytes));

            // First 2 bytes are the command (ushort)
            var command = (ITv2Command)((bytes[0] << 8) | bytes[1]);
            var payload = bytes.Slice(2);

            return DeserializeMessage(command, payload);
        }

        /// <summary>
        /// Deserialize bytes for a known command into a strongly-typed message object.
        /// </summary>
        public static IMessageData DeserializeMessage(ITv2Command command, ReadOnlySpan<byte> payload)
        {
            if (!_commandToType.TryGetValue(command, out var messageType))
            {
                throw new InvalidOperationException(
                    $"No message type registered for command '{command}'. " +
                    $"Ensure the message type is decorated with [ITv2Command({command})].");
            }

            try
            {
                var message = BinarySerializer.Deserialize(messageType, payload);
                if (message is not IMessageData typedMessage)
                {
                    throw new InvalidOperationException(
                        $"Deserialized message type '{messageType.FullName}' does not implement IMessageData.");
                }
                return typedMessage;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize message for command '{command}' into type '{messageType.FullName}'.", ex);
            }
        }

        /// <summary>
        /// Serialize a message object to bytes including the command header.
        /// </summary>
        public static ReadOnlySpan<byte> SerializeMessage(IMessageData message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var messageType = message.GetType();

            if (!_typeToCommand.TryGetValue(messageType, out var command))
            {
                throw new InvalidOperationException(
                    $"No command registered for message type '{messageType.FullName}'. " +
                    $"Ensure the message type is decorated with ITv2CommandAttribute.");
            }

            try
            {
                // Serialize the message payload
                var payloadBytes = BinarySerializer.Serialize(message);
                
                // Prepend the command (ushort, big-endian)
                var commandValue = (ushort)command;
                var result = new byte[2 + payloadBytes.Count];
                result[0] = (byte)(commandValue >> 8);
                result[1] = (byte)(commandValue & 0xFF);
                payloadBytes.CopyTo(result.AsSpan(2));

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to serialize message type '{messageType.FullName}' for command '{command}'.", ex);
            }
        }

        /// <summary>
        /// Serialize just the message payload without the command header.
        /// Used when the command is already in the protocol frame.
        /// </summary>
        public static ReadOnlySpan<byte> SerializeMessagePayload(IMessageData message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var messageType = message.GetType();
            return MemoryPackSerializer.Serialize(messageType, message);
        }

        public static ITv2Command GetCommand(IMessageData message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            var messageType = message.GetType();

            if (_typeToCommand.TryGetValue(messageType, out var command))
            {
                return command;
            }

            throw new InvalidOperationException(
                $"No command registered for message type '{messageType.FullName}'. " +
                $"Ensure the message type is decorated with ITv2CommandAttribute.");
        }

        public static bool CanCreateMessage(ITv2Command command)
        {
            return _commandToType.ContainsKey(command);
        }

        public static Type? GetMessageType(ITv2Command command)
        {
            _commandToType.TryGetValue(command, out var type);
            return type;
        }
    }
}

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session
    {
        static class TransactionFactory
        {
            // Static dictionary mapping message Type -> transaction creator attribute
            private static readonly ImmutableDictionary<Type, ICreateTransaction> _transactionCreators;

            // Static constructor: scan assembly once and populate the lookup
            static TransactionFactory()
            {
                var transactionCreatorsBuilder = ImmutableDictionary.CreateBuilder<Type, ICreateTransaction>();

                var assembly = Assembly.GetExecutingAssembly();

                // Find all types in the assembly that have an attribute implementing ICreateTransaction
                var candidateTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract);

                foreach (var type in candidateTypes)
                {
                    // Get all attributes on the type that implement ICreateTransaction
                    var creatorAttributes = type.GetCustomAttributes(inherit: false)
                        .OfType<ICreateTransaction>()
                        .ToArray();

                    // Register the first matching attribute (or handle multiple if needed)
                    if (creatorAttributes.Length > 0)
                    {
                        transactionCreatorsBuilder[type] = creatorAttributes[0];
                    }
                }
                _transactionCreators = transactionCreatorsBuilder.ToImmutable();
            }

            /// <summary>
            /// Create a transaction for the given message data using the registered attribute factory.
            /// </summary>
            public static ITransaction CreateTransaction(object messageData, ITv2Session session)
            {
                if (messageData == null) throw new ArgumentNullException(nameof(messageData));
                if (session == null) throw new ArgumentNullException(nameof(session));

                var messageType = messageData.GetType();

                // Check for SimpleAckTransaction attribute
                if (messageType.IsDefined(typeof(SimpleAckTransactionAttribute), false))
                {
                    var attr = messageType.GetCustomAttribute<SimpleAckTransactionAttribute>();
                    return new SimpleAckTransaction(session, attr?.Timeout);
                }

                // Check for CommandResponseTransaction attribute
                if (messageType.IsDefined(typeof(CommandResponseTransactionAttribute), false))
                {
                    var attr = messageType.GetCustomAttribute<CommandResponseTransactionAttribute>();
                    return new CommandResponseTransaction(session, attr?.Timeout);
                }

                // Check for HandshakeTransaction attribute
                if (messageType.IsDefined(typeof(HandshakeTransactionAttribute), false))
                {
                    return new HandshakeTransaction(session);
                }

                // Default fallback
                session._log.LogWarning("No transaction attribute found for {MessageType}, using CommandResponseTransaction", 
                    messageType.Name);
                return new CommandResponseTransaction(session);
            }
        }
    }
}

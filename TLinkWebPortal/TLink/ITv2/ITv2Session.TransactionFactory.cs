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

                if (_transactionCreators.TryGetValue(messageType, out var creator))
                {
                    return creator.CreateTransaction(session);
                }

                // Fallback: throw or return a default transaction
                throw new InvalidOperationException(
                    $"No transaction creator attribute found for message type '{messageType.FullName}'. " +
                    $"Ensure the message type is decorated with an attribute implementing ICreateTransaction.");
            }
        }
    }
}

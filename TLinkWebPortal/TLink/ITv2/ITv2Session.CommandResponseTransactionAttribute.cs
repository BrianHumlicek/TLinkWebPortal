namespace DSC.TLink.ITv2
{
    internal partial class ITv2Session
    {
        /// <summary>
        /// Marks a message type as using the CommandResponse transaction pattern.
        /// 
        /// Flow:
        /// 1. Command message (data)
        /// 2. CommandResponse (with status code)
        /// 3. SimpleAck
        /// 4. Complete
        /// 
        /// This is the standard ITv2 transaction pattern for commands that require validation.
        /// </summary>
        /// <example>
        /// [CommandResponseTransaction]
        /// public record RequestZoneStatus : IMessageData
        /// {
        ///     // ... properties
        /// }
        /// </example>
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
        internal sealed class CommandResponseTransactionAttribute : Attribute
        {
            /// <summary>
            /// Optional timeout for the transaction. If null, uses default timeout.
            /// </summary>
            public TimeSpan? Timeout { get; set; }

            public CommandResponseTransactionAttribute()
            {
            }

            /// <summary>
            /// Create a CommandResponse transaction attribute with a custom timeout.
            /// </summary>
            /// <param name="timeoutSeconds">Timeout in seconds</param>
            public CommandResponseTransactionAttribute(int timeoutSeconds)
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            }
        }
    }
}

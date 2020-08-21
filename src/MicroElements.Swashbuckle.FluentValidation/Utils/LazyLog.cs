using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace MicroElements.Swashbuckle.FluentValidation
{
    /// <summary>
    /// Helps to log something only once in some context.
    /// </summary>
    internal class LazyLog
    {
        private readonly Lazy<object> _lazyLog;

        public LazyLog(ILogger logger, Action<ILogger> logAction)
        {
            _lazyLog = new Lazy<object>(() =>
            {
                logAction(logger);
                return new object();
            });
        }

        /// <summary>
        /// Executes log action only once.
        /// </summary>
        public void LogOnce() => IgnoreResult(_lazyLog.Value);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private void IgnoreResult(object obj) {/* empty body. uses for evaluating input arg. */}
    }
}
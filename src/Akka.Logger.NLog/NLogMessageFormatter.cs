using System.Collections.Generic;
using System.Linq;
using Akka.Event;
using NLog;
using NLogLevel = global::NLog.LogLevel;

namespace Akka.Logger.NLog
{
    /// <summary>
    /// This class contains methods used to convert MessageTemplated messages
    /// into normal text messages.
    /// </summary>
    /// <remarks>
    /// You need to enable the Akka.Logger.NLog.NLogMessageFormatter across your entire ActorSystem - this will replace Akka.NET's default ILogMessageFormatter with NLog's.
    /// 
    /// You can accomplish this by setting the akka.logger-formatter setting like below:
    /// <code>
    /// akka.logger-formatter="Akka.Logger.NLog.NLogMessageFormatter, Akka.Logger.NLog"
    /// </code>
    /// </remarks>
    public class NLogMessageFormatter : ILogMessageFormatter
    {
        /// <inheritdoc />
        public string Format(string format, params object[] args)
        {
            if (args?.Length > 0)
            {
                return LogEventInfo.Create(NLogLevel.Info, string.Empty, null, format, args).FormattedMessage;
            }
            return format;
        }

        /// <inheritdoc />
        public string Format(string format, IEnumerable<object> args)
            => Format(format, args.ToArray());
    }
}

//-----------------------------------------------------------------------
// <copyright file="NLogLogger.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Event;
using NLog;
using NLogger = global::NLog.Logger;
using NLogLevel = global::NLog.LogLevel;

namespace Akka.Logger.NLog
{
    /// <summary>
    /// This class is used to receive log events and sends them to
    /// the configured NLog logger. The following log events are
    /// recognized: <see cref="Debug"/>, <see cref="Info"/>,
    /// <see cref="Warning"/> and <see cref="Error"/>.
    /// </summary>
    public class NLogLogger : ReceiveActor, IRequiresMessageQueue<ILoggerMessageQueueSemantics>
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();

        private static void Log(LogEvent logEvent, Action<NLogger, LogEvent> logStatement)
        {
            var loggerName = (logEvent.LogClass == typeof(DummyClassForStringSources) || logEvent.LogClass.GenericTypeArguments?.Length != 0)
                ? logEvent.LogSource
                : logEvent.LogClass.ToString(); // Include full namespace, but not assembly name
            var logger = LogManager.GetLogger(loggerName);
            logStatement(logger, logEvent);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NLogLogger"/> class.
        /// </summary>
        public NLogLogger()
        {
            Receive<Error>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Error, m.LogSource, m.Cause, m.Message)));
            Receive<Warning>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Warn, m.LogSource, logEvent.Message)));
            Receive<Info>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Info, m.LogSource, logEvent.Message)));
            Receive<Debug>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Debug, m.LogSource, logEvent.Message)));
            Receive<InitializeLogger>(m =>
            {
                _log.Info("NLogLogger started");
                Sender.Tell(new LoggerInitialized());
            });
        }

        private static void LogEvent(NLogger logger, NLogLevel level, string logSource, object message)
        {
            LogEvent(logger, level, logSource, null, message);
        }

        private static void LogEvent(NLogger logger, NLogLevel level, string logSource, Exception exception, object message)
        {
            if (logger.IsEnabled(level))
            {
                var logEvent = new LogEventInfo(level, logger.Name, null, "{0}", new[] { message }, exception);
                logEvent.Properties["logSource"] = logSource;   // TODO logSource is the same as logger.Name, now adding twice
                logEvent.Properties["SourceContext"] = Context?.Sender?.Path?.ToString() ?? string.Empty;   // Same as Serilog
                logger.Log(logEvent);
            }
        }
    }
}

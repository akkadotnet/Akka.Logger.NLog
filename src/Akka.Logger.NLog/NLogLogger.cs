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

        private static void Log(LogEvent logEvent, Action<NLogger, string> logStatement)
        {
            var logger = LogManager.GetLogger(logEvent.LogClass.FullName);
            logStatement(logger, logEvent.LogSource);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NLogLogger"/> class.
        /// </summary>
        public NLogLogger()
        {
            Receive<Error>(m => Log(m, (logger, logSource) => LogEvent(logger, NLogLevel.Error, logSource, m.Cause, "{0}", m.Message)));
            Receive<Warning>(m => Log(m, (logger, logSource) => LogEvent(logger, NLogLevel.Warn, logSource, "{0}", m.Message)));
            Receive<Info>(m => Log(m, (logger, logSource) => LogEvent(logger, NLogLevel.Info, logSource, "{0}", m.Message)));
            Receive<Debug>(m => Log(m, (logger, logSource) => LogEvent(logger, NLogLevel.Debug, logSource, "{0}", m.Message)));
            Receive<InitializeLogger>(m =>
            {
                _log.Info("NLogLogger started");
                Sender.Tell(new LoggerInitialized());
            });
        }

        private static void LogEvent(NLogger logger, NLogLevel level, string logSource, string message, params object[] parameters)
        {
            LogEvent(logger, level, logSource, null, message, parameters);
        }

        private static void LogEvent(NLogger logger, NLogLevel level, string logSource, Exception exception, string message, params object[] parameters)
        {
            if (logger.IsEnabled(level))
            {
                var logEvent = new LogEventInfo(level, logger.Name, null, message, parameters, exception);
                logEvent.Properties["logSource"] = logSource;
                logger.Log(logEvent);
            }
        }
    }
}

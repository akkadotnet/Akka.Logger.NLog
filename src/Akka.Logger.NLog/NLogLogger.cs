//-----------------------------------------------------------------------
// <copyright file="NLogLogger.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2016 Typesafe Inc. <http://www.typesafe.com>
//     Copyright (C) 2013-2016 Akka.NET project <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Linq;
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
        private readonly ILoggingAdapter _log = Logging.GetLogger(Context.System.EventStream, "NLogLogger");

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
            Receive<Error>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Error, logEvent.Cause, logEvent)));
            Receive<Warning>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Warn, logEvent.Cause, logEvent)));
            Receive<Info>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Info, logEvent.Cause, logEvent)));
            Receive<Debug>(m => Log(m, (logger, logEvent) => LogEvent(logger, NLogLevel.Debug, logEvent.Cause, logEvent)));
            Receive<InitializeLogger>(m =>
            {
                _log.Info("NLogLogger started");
                Sender.Tell(new LoggerInitialized());
            });
        }

        private static void LogEvent(NLogger logger, NLogLevel level, Exception exception, LogEvent logEvent)
        {
            if (!logger.IsEnabled(level)) 
                return;
            
            var logEventInfo = logEvent.Message is LogMessage logMessage ?
                new LogEventInfo(level, logger.Name, null, logMessage.Format, GetLogMessageParameterArray(logMessage), exception) :
                new LogEventInfo(level, logger.Name, null, "{0}", new object[] { logEvent.Message.ToString() }, exception);
            if (logEventInfo.TimeStamp.Kind == logEvent.Timestamp.Kind)
                logEventInfo.TimeStamp = logEvent.Timestamp;            // Timestamp of original LogEvent (instead of async Logger thread timestamp)
            logEventInfo.Properties["logSource"] = logEvent.LogSource;
            logEventInfo.Properties["actorPath"] = Context?.Sender?.Path?.ToString() ?? string.Empty;   // Same as Serilog
            logEventInfo.Properties["threadId"] = logEvent.Thread.ManagedThreadId;  // ThreadId of the original LogEvent (instead of async Logger threadid)
            logger.Log(logEventInfo);
        }

        private static object[] GetLogMessageParameterArray(LogMessage logMessage)
        {
            var parameters = logMessage.Parameters();
            return parameters is object[] parameterArray ? parameterArray : parameters?.ToArray();
        }
    }
}

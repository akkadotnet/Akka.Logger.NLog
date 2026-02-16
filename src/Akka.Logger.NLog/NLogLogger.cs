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

        /// <summary>
        /// Initializes a new instance of the <see cref="NLogLogger"/> class.
        /// </summary>
        public NLogLogger()
        {
            Receive<Error>(static evt => LogEvent(evt, NLogLevel.Error));
            Receive<Warning>(static evt => LogEvent(evt, NLogLevel.Warn));
            Receive<Info>(static evt => LogEvent(evt, NLogLevel.Info));
            Receive<Debug>(static evt => LogEvent(evt, NLogLevel.Debug));
            Receive<InitializeLogger>(m =>
            {
                _log.Info("NLogLogger started");
                Sender.Tell(new LoggerInitialized());
            });
        }

        private static void LogEvent(LogEvent logEvent, NLogLevel logLevel)
        {
            var loggerName = (logEvent.LogClass == typeof(DummyClassForStringSources) || logEvent.LogClass.GenericTypeArguments?.Length != 0)
                ? logEvent.LogSource
                : logEvent.LogClass.ToString(); // Include full namespace, but not assembly name
            var logger = LogManager.GetLogger(loggerName);
            if (!logger.IsEnabled(logLevel))
                return;

            LogEventInfo logEventInfo = CreateLogEventInfo(logger, logLevel, logEvent);
            if (logEventInfo.TimeStamp.Kind == logEvent.Timestamp.Kind)
                logEventInfo.TimeStamp = logEvent.Timestamp;            // Timestamp of original LogEvent (instead of async Logger thread timestamp)

            // Add Akka metadata properties
            logEventInfo.Properties["logSource"] = logEvent.LogSource;
            var actorPath = Context?.Sender?.Path?.ToString();
            if (!string.IsNullOrEmpty(actorPath))
                logEventInfo.Properties["actorPath"] = actorPath;   // Same as Serilog
            logEventInfo.Properties["threadId"] = logEvent.Thread.ManagedThreadId;  // ThreadId of the original LogEvent (instead of async Logger threadid)

            // Add structured logging properties from semantic logging
            // This enables NLog layouts and targets to access structured properties by name
            if (logEvent.TryGetProperties(out var properties) && properties?.Count > 0)
            {
                foreach (var prop in properties)
                {
                    logEventInfo.Properties[prop.Key] = prop.Value;
                }
            }

            logger.Log(logEventInfo);
        }

        private static LogEventInfo CreateLogEventInfo(NLogger logger, NLogLevel level, LogEvent logEvent)
        {
            if (logEvent.Message is LogMessage logMessage)
            {
                var parameters = logMessage.Parameters();
                var parameterArray = parameters as object[] ?? parameters?.ToArray();
                return new LogEventInfo(level, logger.Name, null, logMessage.Format, parameterArray, logEvent.Cause);
            }

            return new LogEventInfo(level, logger.Name, null, "{0}", new object[] { logEvent.Message }, logEvent.Cause);
        }
    }
}

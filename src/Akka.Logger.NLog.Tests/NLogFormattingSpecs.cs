using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using FluentAssertions;
using FluentAssertions.Extensions;
using NLog;
using NLog.Targets;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Akka.Event.LogLevel;

namespace Akka.Logger.NLog.Tests
{
    public class NLogFormattingSpecs : TestKit.Xunit2.TestKit
    {
        private static readonly Config Config = "akka.loglevel = DEBUG";

        private readonly ILoggingAdapter _loggingAdapter;
        private readonly object _lock = new ();
        const string LogSourceName = "my-test-system";

        public NLogFormattingSpecs(ITestOutputHelper helper) : base(Config, output: helper)
        {
            Config myConfig = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";

            var system = ActorSystem.Create(LogSourceName, myConfig);

            _loggingAdapter = Logging.GetLogger(system.EventStream, system.Name);

            Sys.EventStream.Subscribe(TestActor, typeof(LogEvent));
        }

        [Theory]
        [InlineData(LogLevel.InfoLevel, "test case {0}", new object[] { 1 }, "Info|test case 1")]
        [InlineData(LogLevel.WarningLevel, "test case {0}", new object[] { "2" }, "Warn|test case 2")]
        [InlineData(LogLevel.ErrorLevel, "test case {0}", new object[] { 3.0 }, "Error|test case 3")]
        [InlineData(LogLevel.InfoLevel, "test case {a}", new object[] { 1 }, "Info|test case 1")]
        [InlineData(LogLevel.WarningLevel, "test case {b}", new object[] { "2" }, "Warn|test case \"2\"")]
        [InlineData(LogLevel.ErrorLevel, "test case {c}", new object[] { 3.0 }, "Error|test case 3")]
        public void LoggingTest(LogLevel level, string formatStr, object[] formatArgs, string resultStr)
        {
            var loggingTarget = new MemoryTarget { Layout = "${level}|${message}" };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            AssertLogMessage(
                () => _loggingAdapter.Log(level, formatStr, formatArgs), 
                loggingTarget, resultStr, 5.Seconds()).Should().BeTrue();
        }

        [Theory]
        [InlineData(LogLevel.InfoLevel, "test case {0}", new object[] { 1 }, "{0}|{1}|test case 1")]
        public void LoggingTestWithEventProperties(LogLevel level, string formatStr, object[] formatArgs, string resultStr)
        {
            var loggingTarget = new MemoryTarget
                {Layout = "${event-properties:item=logSource}|${event-properties:item=threadId:format=D4}|${message}" };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            var formattedResultString = string.Format(resultStr, LogSourceName, 
                Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(4, '0'));
            
            AssertLogMessage(
                () => _loggingAdapter.Log(level, formatStr, formatArgs),
                loggingTarget, formattedResultString, 5.Seconds()).Should().BeTrue();
        }

        [Theory]
        [InlineData(LogLevel.InfoLevel, "test {color} case", new object[] { "Red" }, "test {{color}} case|color=Red, logSource={0}, actorPath={1}, threadId={2}")]
        public void LoggingWithStructuredLogging(LogLevel level, string formatStr, object[] formatArgs, string resultStr)
        {
            var loggingTarget = new MemoryTarget { Layout = "${message:raw=true}|${all-event-properties}" };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            var formattedResultString = string.Format(resultStr, LogSourceName, TestActor.Path, 
                Thread.CurrentThread.ManagedThreadId.ToString());
            AssertLogMessage(
                () => _loggingAdapter.Log(level, formatStr, formatArgs), 
                loggingTarget, formattedResultString, 5.Seconds()).Should().BeTrue();
        }

        private bool AssertLogMessage(Action logAction, MemoryTarget target, string expected, TimeSpan timeout)
        {
            Output.WriteLine($"Expected: {expected}");
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                logAction();
                Thread.Sleep(100);
                
                string[] array;
                lock (_lock)
                {
                    array = target.Logs.ToArray();
                    target.Logs.Clear();
                }

                foreach (var s in array)
                {
                    Output.WriteLine($"Logged: {s}");
                    if (s.Equals(expected))
                        return true;
                }
            }
            return false;
        }
    }
}

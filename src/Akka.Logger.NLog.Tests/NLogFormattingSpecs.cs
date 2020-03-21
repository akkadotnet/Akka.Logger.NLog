using System.Linq;
using System.Threading;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Akka.Event.LogLevel;

namespace Akka.Logger.NLog.Tests
{
    public class NLogFormattingSpecs : TestKit.Xunit2.TestKit
    {
        private static readonly Config Config = @"akka.loglevel = DEBUG";

        private readonly ILoggingAdapter _loggingAdapter;
        const string ActorSystemName = "my-test-system";

        public NLogFormattingSpecs(ITestOutputHelper helper) : base(Config, output: helper)
        {
            Config myConfig = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";

            var system = ActorSystem.Create(ActorSystemName, myConfig);

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
            var loggingTarget = new global::NLog.Targets.MemoryTarget { Layout = "${level}|${message}" };
            global::NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(loggingTarget);

            loggingTarget.Logs.Clear();
            _loggingAdapter.Log(level, formatStr, formatArgs);

            for (var i = 0; i < 100; ++i)
            {
                if (loggingTarget.Logs.Count != 0)
                    break;

                Thread.Sleep(10);
            }

            Assert.NotEmpty(loggingTarget.Logs);
            Assert.Equal(resultStr, loggingTarget.Logs.Last());
        }

        [Theory]
        [InlineData(LogLevel.InfoLevel, "test case {0}", new object[] { 1 }, "{0}|{1}|test case 1")]
        public void LoggingTestWithEventProperties(LogLevel level, string formatStr, object[] formatArgs, string resultStr)
        {
            _loggingAdapter.Log(level, formatStr, formatArgs);
            var loggingTarget = new global::NLog.Targets.MemoryTarget
                {Layout = "${event-properties:item=logSource}|${event-properties:item=threadId}|${message}"};
            global::NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(loggingTarget);

            loggingTarget.Logs.Clear();
            _loggingAdapter.Log(level, formatStr, formatArgs);

            for (var i = 0; i < 100; ++i)
            {
                if (loggingTarget.Logs.Count != 0)
                    break;

                Thread.Sleep(10);
            }

            var formattedResultString = string.Format(resultStr, ActorSystemName,
                Thread.CurrentThread.ManagedThreadId.ToString().PadLeft(4, '0'));

            Assert.NotEmpty(loggingTarget.Logs);
            Assert.Equal(formattedResultString, loggingTarget.Logs.Last());
        }
    }
}

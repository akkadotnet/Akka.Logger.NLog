using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using FluentAssertions.Extensions;
using NLog;
using NLog.Targets;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Akka.Event.LogLevel;
using FluentAssertions;

namespace Akka.Logger.NLog.Tests
{
    public class NLogFormattingSpecs : TestKit.Xunit2.TestKit
    {
        private static readonly Config Config = "akka.loglevel = DEBUG";

        private readonly ILoggingAdapter _loggingAdapter;
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
        public async Task LoggingTest(LogLevel level, string formatStr, object[] formatArgs, string resultStr)
        {
            var loggingTarget = new MemoryTarget { Layout = "${level}|${message}" };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            await WaitForRadioSilence(loggingTarget);
            _loggingAdapter.Log(level, formatStr, formatArgs);

            await AwaitConditionAsync(() => loggingTarget.Logs.Count != 0, 3.Seconds(), 10.Milliseconds());

            loggingTarget.Logs.Should().NotBeEmpty();
            loggingTarget.Logs.First().Should().Be(resultStr);
        }

        [Theory]
        [InlineData(LogLevel.InfoLevel, "test case {0}", new object[] { 1 }, "{0}|{1}|test case 1")]
        public async Task LoggingTestWithEventProperties(LogLevel level, string formatStr, object[] formatArgs, string resultStr)
        {
            _loggingAdapter.Log(level, formatStr, formatArgs);
            var loggingTarget = new MemoryTarget
                {Layout = "${event-properties:item=logSource}|${event-properties:item=threadId:format=D4}|${message}" };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            await WaitForRadioSilence(loggingTarget);
            _loggingAdapter.Log(level, formatStr, formatArgs);

            await AwaitConditionAsync(() => loggingTarget.Logs.Count != 0, 3.Seconds(), 10.Milliseconds());

            var formattedResultString = string.Format(resultStr, LogSourceName, "*");

            loggingTarget.Logs.Should().NotBeEmpty();
            loggingTarget.Logs.First().Should().Match(formattedResultString);
        }

        [Theory]
        [InlineData(LogLevel.InfoLevel, "test {color} case", new object[] { "Red" }, "test {{color}} case|color=Red, logSource={0}, actorPath={1}, threadId={2}")]
        public async Task LoggingWithStructuredLogging(LogLevel level, string formatStr, object[] formatArgs, string resultStr)
        {
            var loggingTarget = new global::NLog.Targets.MemoryTarget { Layout = "${message:raw=true}|${all-event-properties}" };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            await WaitForRadioSilence(loggingTarget);
            _loggingAdapter.Log(level, formatStr, formatArgs);

            await AwaitConditionAsync(() => loggingTarget.Logs.Count != 0, 3.Seconds(), 10.Milliseconds());

            var formattedResultString = string.Format(resultStr, LogSourceName, TestActor.Path, "*");

            loggingTarget.Logs.Should().NotBeEmpty();
            loggingTarget.Logs.First().Should().Match(formattedResultString);
        }

        private static async Task WaitForRadioSilence(MemoryTarget target)
        {
            while (target.Logs.Count > 0)
            {
                target.Logs.Clear();
                await Task.Delay(1000);
            }
        }
    }
}

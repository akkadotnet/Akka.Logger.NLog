using System;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Logger.Serilog.Tests
{
    public class NLogFormattingSpecs : TestKit.Xunit2.TestKit
    {
        public static readonly Config Config = @"akka.loglevel = DEBUG";

        private readonly ILoggingAdapter _loggingAdapter;
        private readonly global::NLog.Targets.MemoryTarget _loggingTarget = new global::NLog.Targets.MemoryTarget() { Layout = "${level}|${message}" };

        public NLogFormattingSpecs(ITestOutputHelper helper) : base(Config, output: helper)
        {
            Config myConfig = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";

            var system = ActorSystem.Create("my-test-system", myConfig);

            _loggingAdapter = Logging.GetLogger(system.EventStream, system.Name);

            global::NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(_loggingTarget);

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
            _loggingTarget.Logs.Clear();
            _loggingAdapter.Log(level, formatStr, formatArgs);

            for (int i = 0; i < 100; ++i)
            {
                if (_loggingTarget.Logs.Count != 0)
                    break;

                System.Threading.Thread.Sleep(10);
            }

            Assert.NotEmpty(_loggingTarget.Logs);
            Assert.Equal(resultStr, _loggingTarget.Logs.Last());
        }
    }
}

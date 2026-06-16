using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace Akka.Logger.NLog.Hosting.Tests
{
    /// <summary>
    /// Integration tests that verify Akka log events flow end-to-end through the
    /// Akka.Hosting wiring (AddLogger&lt;NLogLogger&gt;) into a real NLog MemoryTarget.
    /// </summary>
    public class AkkaHostingIntegrationSpecs : IAsyncLifetime
    {
        private IHost? _host;
        private MemoryTarget? _memoryTarget;

        public async Task InitializeAsync()
        {
            // Wire NLog to capture everything in memory for the duration of each test.
            _memoryTarget = new MemoryTarget("memory") { Layout = "${message}" };
            var loggingConfig = new LoggingConfiguration();
            loggingConfig.AddTarget(_memoryTarget);
            loggingConfig.AddRule(global::NLog.LogLevel.Debug, global::NLog.LogLevel.Fatal, _memoryTarget, "*");
            LogManager.Configuration = loggingConfig;

            _host = Host.CreateDefaultBuilder()
                .ConfigureServices((_, services) =>
                {
                    services.AddAkka("NLogHostingTestSystem", cb =>
                    {
                        cb.ConfigureLoggers(lc =>
                        {
                            lc.ClearLoggers();
                            lc.AddLogger<NLogLogger>();
                            lc.LogLevel = Akka.Event.LogLevel.DebugLevel;
                        });
                    });
                })
                .Build();

            await _host.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if (_host is not null)
            {
                await _host.StopAsync();
                _host.Dispose();
            }
        }

        [Fact(DisplayName = "Log events emitted via Akka ILoggingAdapter should appear in NLog MemoryTarget")]
        public async Task AkkaLogEventsShouldReachNLogMemoryTarget()
        {
            var actorSystem = _host!.Services.GetRequiredService<ActorSystem>();
            var logger = Logging.GetLogger(actorSystem.EventStream, "HostingIntegrationTest");

            const string expectedFragment = "Hello from Akka.Hosting NLog integration";
            logger.Info("Hello from Akka.Hosting NLog integration");

            // Give the async Akka log dispatcher time to process the event.
            await Task.Delay(TimeSpan.FromMilliseconds(600));

            var logs = _memoryTarget!.Logs.ToArray();
            logs.Should().NotBeEmpty("the MemoryTarget should have received at least one log entry");
            logs.Should().Contain(
                l => l.Contains(expectedFragment),
                $"expected a log entry containing '{expectedFragment}' but got: {string.Join("; ", logs)}");
        }

        [Fact(DisplayName = "Structured / parameterised Akka log messages should reach NLog MemoryTarget")]
        public async Task StructuredAkkaLogMessagesShouldReachNLogMemoryTarget()
        {
            // Reconfigure MemoryTarget layout to expose structured properties.
            _memoryTarget!.Layout = "${message}|UserId=${event-properties:UserId}";

            var actorSystem = _host!.Services.GetRequiredService<ActorSystem>();
            var logger = Logging.GetLogger(actorSystem.EventStream, "StructuredTest");

            logger.Info("User {UserId} signed in", 42);

            await Task.Delay(TimeSpan.FromMilliseconds(600));

            var logs = _memoryTarget.Logs.ToArray();
            logs.Should().NotBeEmpty("MemoryTarget should have entries after structured log");
            logs.Should().Contain(
                l => l.Contains("UserId=42"),
                $"expected structured property 'UserId=42' but got: {string.Join("; ", logs)}");
        }

        [Fact(DisplayName = "Warning-level Akka log events should reach NLog MemoryTarget")]
        public async Task WarningLevelEventsShouldReachNLogMemoryTarget()
        {
            _memoryTarget!.Layout = "${level:uppercase=true}|${message}";

            var actorSystem = _host!.Services.GetRequiredService<ActorSystem>();
            var logger = Logging.GetLogger(actorSystem.EventStream, "WarnTest");

            logger.Warning("Something looks off: {Reason}", "disk almost full");

            await Task.Delay(TimeSpan.FromMilliseconds(600));

            var logs = _memoryTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            logs.Should().Contain(
                l => l.StartsWith("WARN") && l.Contains("disk almost full"),
                $"expected a WARN entry but got: {string.Join("; ", logs)}");
        }

        [Fact(DisplayName = "Actor-level ILoggingAdapter should route messages through NLogLogger via Akka.Hosting")]
        public async Task ActorLoggingAdapterShouldRouteMessagesViaNLog()
        {
            _memoryTarget!.Layout = "${message}";

            var actorSystem = _host!.Services.GetRequiredService<ActorSystem>();

            // Spawn a transient probe actor that logs one message then stops.
            const string marker = "AKKA_HOSTING_ACTOR_LOG_MARKER";
            var probe = actorSystem.ActorOf(Props.Create(() => new LoggingProbeActor(marker)), "probe");
            probe.Tell("go");

            await Task.Delay(TimeSpan.FromMilliseconds(800));

            var logs = _memoryTarget.Logs.ToArray();
            logs.Should().Contain(
                l => l.Contains(marker),
                $"expected '{marker}' from actor-level logger but got: {string.Join("; ", logs)}");
        }
    }

    /// <summary>
    /// A minimal actor that logs a marker string as soon as it receives any message,
    /// then stops itself.
    /// </summary>
    internal sealed class LoggingProbeActor : ReceiveActor
    {
        public LoggingProbeActor(string marker)
        {
            var log = Logging.GetLogger(Context);
            Receive<string>(_ =>
            {
                log.Info(marker);
                Context.Stop(Self);
            });
        }
    }
}

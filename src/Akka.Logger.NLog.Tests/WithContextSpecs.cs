using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using FluentAssertions;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using LogLevel = Akka.Event.LogLevel;

namespace Akka.Logger.NLog.Tests
{
    /// <summary>
    /// Tests for core Akka.NET WithContext() logging context enrichment
    /// flowing through to NLog LogEventInfo.Properties.
    /// </summary>
    public class WithContextSpecs : IAsyncLifetime
    {
        private static readonly Config Config = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";

        private ActorSystem _sys;
        private ILoggingAdapter _loggingAdapter;

        public Task InitializeAsync()
        {
            _sys = ActorSystem.Create("withcontext-test-system", Config);
            _loggingAdapter = Logging.GetLogger(_sys.EventStream, _sys.Name);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _sys.Terminate();
        }

        [Fact(DisplayName = "WithContext single property should appear in NLog event properties")]
        public void WithContext_SingleProperty_AppearsInNLogEventProperties()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${message}|TenantId=${event-properties:TenantId}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            var contextLogger = _loggingAdapter.WithContext("TenantId", "TENANT-001");
            contextLogger.Info("Processing request");

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();

            if (logs.Any(log => log.Contains("TenantId=TENANT-001")))
                return;

            throw FailException.ForFailure($"Expected TenantId=TENANT-001 in log. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "WithContext multiple properties should all appear in NLog event properties")]
        public void WithContext_MultipleProperties_AllAppear()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "TenantId=${event-properties:TenantId}|CorrelationId=${event-properties:CorrelationId}|Region=${event-properties:Region}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            var contextLogger = _loggingAdapter
                .WithContext("TenantId", "TENANT-002")
                .WithContext("CorrelationId", "CORR-123")
                .WithContext("Region", "us-east-1");
            contextLogger.Info("Multi-context request");

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();

            if (logs.Any(log =>
                log.Contains("TenantId=TENANT-002") &&
                log.Contains("CorrelationId=CORR-123") &&
                log.Contains("Region=us-east-1")))
                return;

            throw FailException.ForFailure($"Expected all three context properties. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "WithContext combined with semantic template should have both context and template properties")]
        public void WithContext_CombinedWithSemanticTemplate_BothAppear()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "TenantId=${event-properties:TenantId}|UserId=${event-properties:UserId}|Action=${event-properties:Action}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            var contextLogger = _loggingAdapter.WithContext("TenantId", "TENANT-003");
            contextLogger.Info("User {UserId} performed {Action}", 42, "login");

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();

            if (logs.Any(log =>
                log.Contains("TenantId=TENANT-003") &&
                log.Contains("UserId=42") &&
                log.Contains("Action=login")))
                return;

            throw FailException.ForFailure($"Expected context + template properties. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "WithContext should not pollute unrelated log events")]
        public void WithContext_DoesNotPolluteUnrelatedLogs()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${message}|SecretContext=${event-properties:SecretContext}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            var contextLogger = _loggingAdapter.WithContext("SecretContext", "should-not-leak");
            var plainLogger = _loggingAdapter;

            contextLogger.Info("Context message");
            plainLogger.Info("Plain message");

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();

            var contextLog = logs.FirstOrDefault(l => l.Contains("Context message"));
            var plainLog = logs.FirstOrDefault(l => l.Contains("Plain message"));

            contextLog.Should().NotBeNull();
            plainLog.Should().NotBeNull();

            contextLog.Should().Contain("SecretContext=should-not-leak");
            // Plain log should NOT have the SecretContext value
            plainLog.Should().NotContain("SecretContext=should-not-leak",
                "context properties should not leak to loggers without that context");
        }
    }
}

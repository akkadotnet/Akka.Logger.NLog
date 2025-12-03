using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using FluentAssertions;
using FluentAssertions.Extensions;
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
    /// Tests for semantic logging functionality added in Akka.NET 1.5.56.
    /// Verifies that structured properties from log message templates are
    /// accessible in NLog's LogEventInfo.Properties dictionary.
    /// </summary>
    public class SemanticLoggingSpecs: IAsyncLifetime
    {
        private static readonly Config Config = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";
        
        private ActorSystem _sys;
        private ILoggingAdapter _loggingAdapter;

        public Task InitializeAsync()
        {
            _sys = ActorSystem.Create("semantic-test-system", Config);
            _loggingAdapter = Logging.GetLogger(_sys.EventStream, _sys.Name);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await _sys.Terminate();
        }

        [Fact(DisplayName = "Should extract named template properties and add to NLog LogEventInfo.Properties")]
        public void NamedTemplatePropertiesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${message}|UserId=${event-properties:UserId}|Email=${event-properties:Email}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            _loggingAdapter.Info("User {UserId} with email {Email} logged in", 12345, "user@example.com");

            Thread.Sleep(500); // Give logger time to process

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            
            if (logs.Any(log => log.Contains("UserId=12345") && log.Contains("Email=user@example.com")))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "Should extract positional template properties and add to NLog LogEventInfo.Properties")]
        public void PositionalTemplatePropertiesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${message}|Param0=${event-properties:0}|Param1=${event-properties:1}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            _loggingAdapter.Info("User {0} logged in from {1}", "Bob", "192.168.1.1");

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            
            if (logs.Any(log => log.Contains("Param0=Bob") && log.Contains("Param1=192.168.1.1")))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "Should handle multiple named properties in template")]
        public void MultipleNamedPropertiesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${event-properties:OrderId}|${event-properties:CustomerId}|${event-properties:Amount}|${event-properties:Currency}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            _loggingAdapter.Info("Order {OrderId} for customer {CustomerId}: {Amount} {Currency}",
                "ORD-001", "CUST-456", 99.99, "USD");

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            
            if (logs.Any(log => log == "ORD-001|CUST-456|99.99|USD"))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "Should handle complex objects as property values")]
        public void ComplexObjectPropertiesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${message}|UserType=${event-properties:User:objectpath=GetType().Name}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            var user = new { Name = "Alice", Age = 30 };
            _loggingAdapter.Info("Processing user {User}", user);

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            
            if (logs.Any(log => log.Contains("Processing user")))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "Should preserve Akka metadata properties alongside semantic logging properties")]
        public void AkkaMetadataAndSemanticPropertiesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "UserId=${event-properties:UserId}|LogSource=${event-properties:logSource}|ThreadId=${event-properties:threadId}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            _loggingAdapter.Info("User {UserId} action", 999);

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            
            var regex = new Regex(@"ThreadId=\d+");
            // Should have both semantic property (UserId) and Akka metadata (logSource, threadId)
            if (logs.Any(log => log.Contains("UserId=999") && log.Contains("LogSource=semantic-test-system") && regex.IsMatch(log)))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "Should handle format specifiers in named templates")]
        public void FormatSpecifiersInTemplatesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${event-properties:Amount}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            // Template has format specifier :N2, but property name should be "Amount" (specifier removed)
            _loggingAdapter.Info("Total amount: {Amount:N2}", 1234.5678);

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            
            if (logs.Any(log => log.Contains("1234.5678")))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "Should handle empty/no properties gracefully")]
        public void NoPropertiesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${message}|Props=${all-event-properties}"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            _loggingAdapter.Info("No template properties here");

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();
            
            // Should still have Akka metadata properties (logSource, actorPath, threadId)
            if (logs.Any(log => log.Contains("logSource=")))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }

        [Fact(DisplayName = "Should make all properties queryable via ${all-event-properties}")]
        public void AllEventPropertiesTest()
        {
            var loggingTarget = new MemoryTarget
            {
                Layout = "${all-event-properties:separator=, }"
            };
            LogManager.Setup().LoadConfiguration(c => c.ForLogger().WriteTo(loggingTarget));

            _loggingAdapter.Info("Event {EventId} at {Timestamp}", "EVT-123", DateTime.UtcNow);

            Thread.Sleep(500);

            var logs = loggingTarget.Logs.ToArray();
            logs.Should().NotBeEmpty();

            if (logs.Any(log => 
                    // Should contain semantic properties
                    log.Contains("EventId=EVT-123") && log.Contains("Timestamp=") 
                    // Should contain Akka metadata
                    && log.Contains("logSource=") && log.Contains("threadId=")))
                return;
            
            throw FailException.ForFailure($"Expected log not found. Logs:\n{string.Join('\n', logs)}");
        }
    }
}

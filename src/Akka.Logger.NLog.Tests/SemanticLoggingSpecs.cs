using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using LogLevel = Akka.Event.LogLevel;

namespace Akka.Logger.NLog.Tests
{
    /// <summary>
    /// Tests for semantic logging functionality added in Akka.NET 1.5.56.
    /// Verifies that structured properties from log message templates are
    /// accessible in NLog's LogEventInfo.Properties dictionary.
    /// </summary>
    public class SemanticLoggingSpecs : TestKit.Xunit2.TestKit
    {
        private static readonly Config Config = "akka.loglevel = DEBUG";
        private readonly ILoggingAdapter _loggingAdapter;

        public SemanticLoggingSpecs(ITestOutputHelper helper) : base(Config, output: helper)
        {
            var target = new TestOutputTarget(helper);
            var config = new LoggingConfiguration();
            config.AddRuleForAllLevels(target);
            LogManager.Configuration = config;
                
            Config myConfig = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";

            var system = ActorSystem.Create("semantic-test-system", myConfig);
            _loggingAdapter = Logging.GetLogger(system.EventStream, system.Name);
            Sys.EventStream.Subscribe(TestActor, typeof(LogEvent));
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
            logs[0].Should().Contain("UserId=12345");
            logs[0].Should().Contain("Email=user@example.com");
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
            logs[0].Should().Contain("Param0=Bob");
            logs[0].Should().Contain("Param1=192.168.1.1");
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
            logs[0].Should().Be("ORD-001|CUST-456|99.99|USD");
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
            logs[0].Should().Contain("Processing user");
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
            // Should have both semantic property (UserId) and Akka metadata (logSource, threadId)
            logs[0].Should().Contain("UserId=999");
            logs[0].Should().Contain("LogSource=semantic-test-system");
            logs[0].Should().MatchRegex(@"ThreadId=\d+");
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
            logs[0].Should().Contain("1234.5678");
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
            logs[0].Should().Contain("logSource=");
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
            var output = logs[0];

            // Should contain semantic properties
            output.Should().Contain("EventId=EVT-123");
            output.Should().Contain("Timestamp=");

            // Should contain Akka metadata
            output.Should().Contain("logSource=");
            output.Should().Contain("threadId=");
        }
    }
}

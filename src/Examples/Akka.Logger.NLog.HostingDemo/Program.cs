using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Logger.NLog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;

// -----------------------------------------------------------------------
// Configure NLog to write structured output to the console.
// -----------------------------------------------------------------------
var loggingConfig = new LoggingConfiguration();
var consoleTarget = new ConsoleTarget("console")
{
    Layout = Layout.FromString(
        "${date:format=HH\\:mm\\:ss.fff} [${level:uppercase=true}] ${logger} - ${message}" +
        "${onexception:${newline}${exception:format=tostring}}" +
        " | source=${event-properties:logSource}" +
        " | thread=${event-properties:threadId}")
};
loggingConfig.AddTarget(consoleTarget);
loggingConfig.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, consoleTarget, "*");
LogManager.Configuration = loggingConfig;

// -----------------------------------------------------------------------
// Build the generic host, wire Akka.Hosting + NLogLogger.
// -----------------------------------------------------------------------
var host = Host.CreateDefaultBuilder(args)
    .UseConsoleLifetime()
    .ConfigureServices((_, services) =>
    {
        services.AddAkka("NLogDemoSystem", cb =>
        {
            cb.ConfigureLoggers(lc =>
            {
                lc.ClearLoggers();               // drop Akka's default console logger
                lc.AddLogger<NLogLogger>();       // route Akka log events into NLog
                lc.LogLevel = Akka.Event.LogLevel.DebugLevel;
            });

            // Register a simple greeter actor so we can generate log messages.
            cb.WithActors((system, registry) =>
            {
                var greeter = system.ActorOf(Props.Create<GreeterActor>(), "greeter");
                registry.Register<GreeterActor>(greeter);
            });
        });

        // Add a hosted service that exercises the actor and then shuts down.
        services.AddHostedService<DemoRunner>();
    })
    .Build();

await host.RunAsync();

// -----------------------------------------------------------------------
// Actor that logs structured messages when it receives greet requests.
// -----------------------------------------------------------------------
public sealed class GreeterActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Logging.GetLogger(Context);
    private int _count;

    public GreeterActor()
    {
        Receive<string>(name =>
        {
            _count++;
            // Structured / parameterised log – verifies template forwarding.
            _log.Info("Greeting #{Count} sent to {Name} from actor {ActorPath}",
                _count, name, Self.Path);
        });

        Receive<int>(n =>
        {
            _log.Debug("Received numeric message: {Value}", n);
        });
    }
}

// -----------------------------------------------------------------------
// IHostedService that drives the demo, then requests host shutdown.
// -----------------------------------------------------------------------
public sealed class DemoRunner : IHostedService
{
    private readonly ActorRegistry _registry;
    private readonly IHostApplicationLifetime _lifetime;

    public DemoRunner(ActorRegistry registry, IHostApplicationLifetime lifetime)
    {
        _registry = registry;
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var greeter = _registry.Get<GreeterActor>();

        greeter.Tell("Alice");
        greeter.Tell("Bob");
        greeter.Tell(42);
        greeter.Tell("Charlie");

        // Give Akka's async log dispatcher a moment to flush all messages.
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

        _lifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

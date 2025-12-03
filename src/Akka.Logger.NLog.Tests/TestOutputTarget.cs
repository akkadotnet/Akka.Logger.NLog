using NLog;
using NLog.Targets;
using Xunit.Abstractions;

namespace Akka.Logger.NLog.Tests;

public class TestOutputTarget : TargetWithLayout 
{
    private readonly ITestOutputHelper Output;

    public TestOutputTarget(ITestOutputHelper output) {
        Output = output;
    }

    protected override void Write(LogEventInfo logEvent) {
        Output.WriteLine(RenderLogEvent(Layout, logEvent));
    }
}
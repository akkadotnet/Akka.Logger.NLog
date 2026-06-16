# Akka.Logger.NLog
[![Build status](https://dev.azure.com/dotnet/Akka.NET/_apis/build/status/189)](https://dev.azure.com/dotnet/Akka.NET/_build?definitionId=189) [![NuGet Version](http://img.shields.io/nuget/v/Akka.Logger.NLog.svg?style=flat)](https://www.nuget.org/packages/Akka.Logger.NLog/)

This is the NLog integration plugin for Akka.NET.

## Configuration

### Configuration via code
```C#
// Step 1. Create configuration object 
var config = new NLog.Config.LoggingConfiguration();

// Step 2. Create targets and configure properties
var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
logconsole.Layout = @"${date:format=HH\:mm\:ss} ${level} ${logger} ${message}";

// Step 3. Define filtering rules
config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            
// Step 4. Activate the configuration         
NLog.LogManager.Configuration = config;

Config myConfig = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";

var system = ActorSystem.Create("my-test-system", myConfig);
```

## Configuration via NLog.config file
Add NLog.config file to your project
```xml
﻿<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets>
    <target name="console" xsi:type="Console" layout="[${logger}] [${level:uppercase=true}] [${event-properties:item=logSource}] [${event-properties:item=actorPath}] [${event-properties:item=threadId:format=D4}] : ${message}"/>
  </targets>
  <rules>
    <logger name="*" minlevel="Debug" writeTo="console"/>
  </rules>
</nlog>
```

Change your *.csproj file with this content
```xml
<ItemGroup>
  <None Include="NLog.config">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

Change your Akka.NET configuration
```C#
Config myConfig = @"akka.loglevel = DEBUG
                    akka.loggers=[""Akka.Logger.NLog.NLogLogger, Akka.Logger.NLog""]";

var system = ActorSystem.Create("my-test-system", myConfig);
```

### Configuration via Akka.Hosting

NLog can be registered as the Akka.NET logger through Akka.Hosting using the `ConfigureLoggers` builder method:

```csharp
using Akka.Hosting;
using Akka.Logger.NLog;

// builder is a HostApplicationBuilder or WebApplicationBuilder
builder.Services.AddAkka("MySystem", configurationBuilder =>
{
    configurationBuilder.ConfigureLoggers(loggerConfigBuilder =>
    {
        loggerConfigBuilder.ClearLoggers();           // remove Akka's default console logger
        loggerConfigBuilder.AddLogger<NLogLogger>();  // route Akka events into NLog
    });
});
```

NLog targets and rules are still configured as usual—either via `NLog.config` file or programmatically (both methods are shown in the sections above).

For a complete runnable example, see the demo project at `src/Examples/Akka.Logger.NLog.HostingDemo`.

## Maintainer
- Akka.NET Team

#### 1.5.59 January 26th 2026 ####

**Dependency Updates**
* [Upgraded to Akka.NET v1.5.59](https://github.com/akkadotnet/akka.net/releases/tag/1.5.59)

#### 1.5.57-beta2 December 4th 2025 ####

**New Features**
* [Add semantic logging support for Akka.NET 1.5.56+](https://github.com/akkadotnet/Akka.Logger.NLog/pull/242) - NLog layouts can now access structured properties using `${event-properties:PropertyName}` syntax
* [Support custom ILogMessageFormatter implementations](https://github.com/akkadotnet/Akka.Logger.NLog/pull/196) - Refactored NLogLogger to allow override of ILogMessageFormatter

**Performance Improvements**
* [Optimize logging of LogMessage when Parameters() is object-array](https://github.com/akkadotnet/Akka.Logger.NLog/pull/186)

**Dependency Updates**
* [Upgraded to Akka.NET v1.5.57-Beta2](https://github.com/akkadotnet/akka.net/releases/tag/1.5.57-Beta2)
* [Upgraded to NLog v6.0.6](https://github.com/NLog/NLog/releases/tag/v6.0.6)

#### 1.5.41 May 15 2025 ####

* [NLogLogger - Optimize logging of LogMessage when Parameters() is object-array ](https://github.com/akkadotnet/Akka.Logger.NLog/pull/186)
* [Upgraded to Akka.NET v1.5.41](https://github.com/akkadotnet/akka.net/releases/tag/1.5.41)
* [Upgraded to NLog v5.4.0](https://github.com/NLog/NLog/releases/tag/v5.4.0)
